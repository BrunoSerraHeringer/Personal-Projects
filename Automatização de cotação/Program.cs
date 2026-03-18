using System; // Contém funções básicas
using System.Net.Mail; // Usada para enviar e-mails via SMTP
using System.Net; // Necessário para autenticar o servidor SMTP
using System.Net.Http; // Usado para acessar a API de cotação
using System.Text.Json; // Usado para ler o JSON retornado pela API
using System.Threading.Tasks; //Necessária para operações assíncronas e o intervalo de loop (async/await)
using Microsoft.Extensions.Configuration; // Não necessária, mas lê o arquivo settings.json de forma profissional 

class Program
{
    static async Task Main(string[] args)
    {
        // O programa precisa receber EXATAMENTE 3 parâmetros:
        // - asset
        // - preço de venda
        // - preço de compra
        // Caso não receba, mostra a forma certa de usar.
        if (args.Length != 3)
        {
            Console.WriteLine("Uso correto:");
            Console.WriteLine("stock-quote-alert.exe <ATIVO> <REFERENCIA-VENDA> <REFERENCIA-COMPRA>");
            return;
        }

        // Pega os argumentos da linha de comando
        string asset = args[0].ToUpper(); // Boa prática para deixar o asset em maiúsculo caso o usuário não coloque
        // traduzindo o texto para decimal
        decimal sale = decimal.Parse(args[1].Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);
        decimal buy  = decimal.Parse(args[2].Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture);

        // Carrega o arquivo settings.json
        // Esse arquivo contém: SMTP, e-mail destino, API key, etc.
        // Boa prática para facilitar alterar os dados e manter as informações essenciais mais secretas
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)// Procura o settings.json, caso não ache, fecha o programa
            .Build();

        // Lê as configurações do e-mail
        string emailDestination = config["EmailSettings:To"];
        string emailOrigin = config["EmailSettings:From"];
        string smtpHost = config["EmailSettings:SmtpHost"];
        int smtpPort = int.Parse(config["EmailSettings:SmtpPort"]);
        string smtpUser = config["EmailSettings:Username"];
        string smtpPassword = config["EmailSettings:Password"];
        bool enableSsl = bool.Parse(config["EmailSettings:EnableSsl"]);

        // Lê a chave da API e o intervalo de consulta
        string apiKey = config["ApiKey"];
        int intervalSeconds = int.Parse(config["IntervalSeconds"]); // Delay

        // 3. Informações iniciais na tela
        Console.WriteLine($"Monitorando {asset}...");
        Console.WriteLine($"Venda acima de {sale}");
        Console.WriteLine($"Compra abaixo de {buy}");
        Console.WriteLine("Pressione CTRL + C para parar.\n");

        // HttpClient será usado para consultar a API de cotação
        using var http = new HttpClient();// Boa prática com using para fechar a conexão corretamente, mesmo com um erro no caminho

        // LOOP INFINITO DE MONITORAMENTO
        // O programa fica rodando até ser encerrado.
        while (true)
        {
            try// Escudo no código que pode falhar
            {
                // Consulta o preço atual do asset
                decimal price = await  GetQuote(http, asset, apiKey);
                Console.WriteLine($"{DateTime.Now} → {asset}: {price}");

                // Verifica se o preço disparou alerta de VENDA
                if (price > sale)
                {
                    SendEmail(
                        smtpHost, smtpPort, smtpUser, smtpPassword, enableSsl,
                        emailOrigin, emailDestination,
                        $"Alerta: Venda {asset}",
                        $"O preço do asset {asset} atingiu {price}, acima do nível de venda {sale}."
                    );
                }

                // Verifica se o preço disparou alerta de COMPRA
                if (price < buy)
                {
                    SendEmail(
                        smtpHost, smtpPort, smtpUser, smtpPassword, enableSsl,
                        emailOrigin, emailDestination,
                        $"Alerta: Compra {asset}",
                        $"O preço do asset {asset} caiu para {price}, abaixo do nível de compra {buy}."
                    );
                }
            }
            catch (Exception ex)
            {
                // Se acontecer qualquer erro, mostra mas não interrompe a execução
                Console.WriteLine($"Erro: {ex.Message}");
            }

            // Aguarda X segundos antes da próxima consulta
            await Task.Delay(intervalSeconds * 1000);
        }
    }

    // Função para consultar a cotação na API Alpha Vantage
    // Retorna um decimal com o preço atual do ativo
    static async Task<decimal> GetQuote(HttpClient http, string asset, string apiKey)
    {
        // Monta a URL da API
        string url = $"https://www.alphavantage.co/query?function=GLOBAL_QUOTE&symbol={asset}.SA&apikey={apiKey}";

        // Faz a requisição GET
        string json = await http.GetStringAsync(url);

        // Lê o JSON retornado
        using var doc = JsonDocument.Parse(json);

        // Extrai o valor do campo "05. price"
        string priceStr = doc.RootElement
            .GetProperty("Global Quote")
            .GetProperty("05. price")
            .GetString();

        // Converte de string para decimal e retorna
        return decimal.Parse(priceStr, System.Globalization.CultureInfo.InvariantCulture);
    }

    // Função que envia e-mail usando SMTP
    static void SendEmail(
        string host, int port, string user, string pass, bool ssl,
        string from, string to, string subject, string message)
    {
        using var client = new SmtpClient(host, port)
        {
            Credentials = new NetworkCredential(user, pass),
            EnableSsl = ssl
        };

        // Monta o e-mail
        var mail = new MailMessage(from, to, subject, message);

        client.Send(mail);

        Console.WriteLine($"[EMAIL ENVIADO] {subject}");
    }
}