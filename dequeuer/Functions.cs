using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json.Linq;

namespace dequeuer
{
    public static class Functions
    {
        [FunctionName("nmCommandDequeuer")]
        public static async Task RunHrmCommandDequeuer(
            [ServiceBusTrigger("command-nm", AccessRights.Listen, Connection = "sbConnection")]
            BrokeredMessage message,
            TraceWriter log)
        {
            await SendMessage(message, log, "notifications/messages/inbox/commands");
        }

        private static async Task SendMessage(
            BrokeredMessage message,
            TraceWriter log,
            string messageInbox)
        {
            log.Info("C# ServiceBus queue trigger function processed message");

            using (HttpClient httpClient = new HttpClient())
            using (HttpRequestMessage request = new HttpRequestMessage())
            using (ByteArrayContent byteArrayContent = new ByteArrayContent(Encoding.UTF8.GetBytes(message.GetBody<string>())))
            {
                string baseUri = Environment.GetEnvironmentVariable("apiBaseAddress") ?? throw new InvalidOperationException("apiBaseAddress was null");
                httpClient.BaseAddress = new Uri(baseUri);

                byteArrayContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                request.RequestUri = new Uri(messageInbox, UriKind.Relative);
                request.Method = HttpMethod.Post;
                request.Content = byteArrayContent;
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetToken());

                string tenantCode = message.Properties.ContainsKey("TenantCode") ? message.Properties["TenantCode"].ToString() : throw new InvalidOperationException("Tenant code not found on message");
                string environmentCode = message.Properties.ContainsKey("EnvironmentCode") ? message.Properties["EnvironmentCode"].ToString() : throw new InvalidOperationException("Environment code not found on message");
                string correlationId = message.Properties.ContainsKey("CorrelationId") ? message.Properties["CorrelationId"].ToString() : throw new InvalidOperationException("Correlation Id not found on message");
                string messageVersion = message.Properties.ContainsKey("MessageVersion") ? message.Properties["MessageVersion"].ToString() : null;

                request.Headers.Add("TenantCode", tenantCode);
                request.Headers.Add("EnvironmentCode", environmentCode);
                request.Headers.Add("CorrelationId", correlationId);
                request.Headers.Add("MessageVersion", messageVersion);

                HttpResponseMessage response = await httpClient.SendAsync(request);

                log.Info(FormattableString.Invariant($"Message [{message.Properties["PFMessageId"].ToString()}] posted, and got response [{response.StatusCode}]"));

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new InvalidOperationException(FormattableString.Invariant($"Message [{message.Properties["PFMessageId"].ToString()}] was not processed"));
                }

                await message.CompleteAsync();
            }
        }

        private static async Task<string> GetToken()
        {
            string resource = Environment.GetEnvironmentVariable("authResource");
            string clientId = Environment.GetEnvironmentVariable("authclientId"); ;
            string tokenEndpoint = Environment.GetEnvironmentVariable("authTokenEndpoint"); ;

            KeyVaultClient keyVaultClient = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(new AzureServiceTokenProvider().KeyVaultTokenCallback));
            SecretBundle secretAsync = await keyVaultClient.GetSecretAsync("service-account-secret");
            string clientSecret = secretAsync.Value;

            string requestString = FormattableString.Invariant($"grant_type=client_credentials&resource={HttpUtility.UrlEncode(resource)}&client_id={HttpUtility.UrlEncode(clientId)}&client_secret={HttpUtility.UrlEncode(clientSecret)}");
            byte[] requestData = new ASCIIEncoding().GetBytes(requestString);

            using (HttpClient httpClient = new HttpClient())
            using (HttpRequestMessage request = new HttpRequestMessage())
            using (ByteArrayContent byteArrayContent = new ByteArrayContent(requestData))
            {
                string baseUri = Environment.GetEnvironmentVariable("apiBaseAddress") ?? throw new InvalidOperationException("apiBaseAddress was null");
                httpClient.BaseAddress = new Uri(baseUri);

                byteArrayContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                request.RequestUri = new Uri(tokenEndpoint, UriKind.Absolute);
                request.Method = HttpMethod.Post;
                request.Content = byteArrayContent;
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetToken());

                HttpResponseMessage response = await httpClient.SendAsync(request);

                return JObject.Parse(await response.Content.ReadAsStringAsync()).SelectToken("access_token").ToString();
            }
        }
    }
}