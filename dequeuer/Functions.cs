using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Web;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json.Linq;

namespace dequeuer
{
    public static class Functions
    {
        [FunctionName("dequeuer")]
        public static void Run(
        [ServiceBusTrigger("test1", AccessRights.Manage, Connection = "sbConnection")]
            BrokeredMessage message,
            TraceWriter log)
        {
            log.Info("C# ServiceBus queue trigger function processed message");

            using(HttpClient httpClient = new HttpClient())
            using (HttpRequestMessage request = new HttpRequestMessage())
            using (ByteArrayContent byteArrayContent = new ByteArrayContent(Encoding.UTF8.GetBytes(message.GetBody<string>())))
            {
                byteArrayContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                request.RequestUri = new Uri("/api/messages");
                request.Method = HttpMethod.Post;
                request.Content = byteArrayContent;
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", GetToken());
                request.Headers.Add("TenantCode", message.Properties["TenantCode"].ToString());
                request.Headers.Add("EnvironmentCode", message.Properties["EnvironmentCode"].ToString());
                request.Headers.Add("CorrelationId", message.Properties["CorrelationId"].ToString());
                request.Headers.Add("MessageVersion", message.Properties.ContainsKey("MessageVersion") ? message.Properties["MessageVersion"].ToString() : null);

                HttpResponseMessage response = httpClient.SendAsync(request).Result;

                log.Info(FormattableString.Invariant($"Message [{message.Properties["MessageId"].ToString()}] posted, and got response [{response.StatusCode}]"));

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new InvalidOperationException(FormattableString.Invariant($"Message [{message.Properties["MessageId"].ToString()}] was not processed"));
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times", Justification = "Disposing of objects is happening correctly.")]
        public static string GetToken()
        {
            string resource = Environment.GetEnvironmentVariable("authResource");
            string clientId = Environment.GetEnvironmentVariable("authclientId"); ;
            string clientSecret = Environment.GetEnvironmentVariable("authClientSecret"); ;
            string tokenEndpoint = Environment.GetEnvironmentVariable("authTokenEndpoint"); ;

            string request = FormattableString.Invariant($"grant_type=client_credentials&resource={HttpUtility.UrlEncode(resource)}&client_id={HttpUtility.UrlEncode(clientId)}&client_secret={HttpUtility.UrlEncode(clientSecret)}");
            byte[] requestData = new ASCIIEncoding().GetBytes(request);

            HttpWebRequest webRequest = WebRequest.CreateHttp(new Uri(tokenEndpoint));

            webRequest.Method = "POST";
            webRequest.ContentType = "application/x-www-form-urlencoded";
            webRequest.ContentLength = requestData.Length;

            using (Stream stream = webRequest.GetRequestStream())
            {
                stream.Write(requestData, 0, requestData.Length);
            }

            HttpWebResponse httpWebResponse = (HttpWebResponse)webRequest.GetResponse();

            using (Stream responseStream = httpWebResponse.GetResponseStream())
            {
                using (StreamReader reader = new StreamReader(responseStream))
                {
                    string response = reader.ReadToEnd();
                    return JObject.Parse(response).SelectToken("access_token").ToString();
                }
            }
        }
    }
}