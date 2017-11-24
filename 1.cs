using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using CommandLine;
using CommandLine.Text;
using Newtonsoft.Json;
using System.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;

namespace TM1_Maximo_Service
{
    class Program
    {

        public bool Debug = true;



        static void Main(string[] args)
        {

            var options = new Options();
            if (Parser.Default.ParseArguments(args, options))
            {
                try
                {
                    var processor = new Processor(options);

                    System.Console.WriteLine("Starting Process..");

                    processor.process();
                }
                catch (Exception e)
                {
                    System.Console.WriteLine("Error:" + e.Message + e.StackTrace);

                }

                System.Console.WriteLine("End Processing");
            }
        }
    }

    class Processor
    {
        private string tmUrl;
        private string tmUser;
        private string tmPassword;
        private string tmCube;
        private string tmView;

        private string maxURL;
        private string maxUser;
        private string maxPassword;

        public bool Debug = false;

        public string cellSetID;
        public List<Cell> allCells = new List<Cell>();

        public Processor(Options options)
        {
            // TM1 arguments declaration
            if (options.urlTM1 == string.Empty)
            {
                this.tmUrl = "https://sr-dc-009:10001/api/v1";
            }
            else
            {
                this.tmUrl = options.urlTM1 as string;
            }

            if (options.userNameTM1 == string.Empty)
            {
                this.tmUser = "Admin";
            }
            else
            {
                this.tmUser = options.userNameTM1 as string;
            }

            if (options.passwordTM1 == string.Empty)
            {
                this.tmPassword = "";
            }
            else
            {
                this.tmPassword = options.passwordTM1 as string;
            }

            if (options.cubeTM1 == string.Empty)
            {
                this.tmCube = "04.01.PrognosUchetTPiR{Company";
            }
            else
            {
                this.tmCube = options.cubeTM1 as string;
            }

            if (options.viewTM1 == string.Empty)
            {
                this.tmView = "StatusForMaxima";
            }
            else
            {
                this.tmView = options.viewTM1 as string;
            }

            //Maximo arguments declaration

            if (options.usernameMaximo == string.Empty)
            {
                this.maxUser = "maxadmin";
            }
            else
            {
                this.maxUser = options.usernameMaximo as string;
            }

            if (options.passwordMaximo == string.Empty)
            {
                this.maxPassword = "mjnhbg543";
            }
            else
            {
                this.maxPassword = options.passwordMaximo as string;
            }

            if (options.maximoUrl == string.Empty)
            {
                this.maxURL = "http://10.1.3.8/maxrest/rest/os/mxwo";
            }
            else
            {
                this.maxURL = options.maximoUrl as string;
            }

            // Debug

            if (options.DebugOn)
            {
                this.Debug = true;
            }
            else
            {
                this.Debug = false;
            }

        }

        public void process()
        {
            bypassCert();
            var task = API.getFromTM(tmUrl, tmUser, tmPassword, tmCube, tmView);
            analyzeTM(JsonConvert.DeserializeObject<Data>(task.Result));
        }

        private void analyzeTM(Data data)
        {
            cellSetID = data.ID;
            allCells = data.Cells;
            List<Tuple<string, string, string, int>> arrayForMaximo = new List<Tuple<string, string, string, int>>();
            for (int i = 0; i < allCells.Count; i += 3)
            {
                var workerID = allCells[i].Value;
                var status = allCells[i + 1].Value;
                var flag = allCells[i + 2].Value;
                var fOrder = allCells[i + 2].Ordinal;
                Tuple<string, string, string, int> trippleTuple = Tuple.Create(workerID, status, flag, fOrder);
                arrayForMaximo.Add(trippleTuple);
            }
            sendToMaximo(arrayForMaximo);
        }

        private void sendToMaximo(List<Tuple<string, string, string, int>> stack)
        {
            for (int i = 0; i < stack.Count; i++)
            {
                Tuple<string, string, string, int> tuple = stack[i];
                if (tuple.Item3 == string.Empty)
                {
                    if (tuple.Item1 != string.Empty || tuple.Item2 != string.Empty)
                    {
                        var task = API.sendToMaximo(maxURL, maxUser, maxPassword, tuple.Item1, tuple.Item2);
                        var resultString = task.Result;
                        if (resultString != string.Empty)
                        {
                            closeWorkInTM(tuple.Item4);
                        }
                    }
                }
            }
        }

        private void closeWorkInTM(int order)
        {
            API.sendToTM1(tmUrl, tmUser, cellSetID, order);
        }

        private void bypassCert()
        {
            ServicePointManager.ServerCertificateValidationCallback = new System.Net.Security.RemoteCertificateValidationCallback(API.AcceptAllCertifications);
            /* System.Net.ServicePointManager.ServerCertificateValidationCallback += delegate (object sender,
                        System.Security.Cryptography.X509Certificates.X509Certificate certificate,
                        System.Security.Cryptography.X509Certificates.X509Chain chain,
                        System.Net.Security.SslPolicyErrors sslPolicyErrors
                        )
            { return true; }; // Always Accept */

        }

    }

    class API
    {

        public static bool AcceptAllCertifications(object sender, System.Security.Cryptography.X509Certificates.X509Certificate certification, System.Security.Cryptography.X509Certificates.X509Chain chain, System.Net.Security.SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

        private static string ReadStreamFromResponse(WebResponse response)
        {
            using (Stream responseStream = response.GetResponseStream())
            using (StreamReader sr = new StreamReader(responseStream))
            {
                var statusCode = ((HttpWebResponse)response).StatusCode.ToString();
                //Need to return this response 
                string strContent = sr.ReadToEnd();

                return strContent;
            }
        }

        public static Task<string> sendToTM1(string url, string user, string id, int order)
        {

            string fullUrl = $"{url}/Cellsets('{id}')/Cells";

            var userBase64 = Base64Encode($"{user}:");

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(new Uri(fullUrl));
            request.Method = "PATCH";
            request.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
            request.Headers.Add("Authorization", $"Basic {userBase64}");
            request.ContentType = "application/json";
            request.AllowAutoRedirect = false;
            X509Certificate cert1 = request.ServicePoint.Certificate;
            X509Certificate cert2 = new X509Certificate(cert1);
            string cn = cert2.Issuer;
            string cedate = cert2.GetExpirationDateString();
            string cpub = cert2.GetPublicKeyString();
            System.Console.WriteLine(cn);
            System.Console.WriteLine(cedate);
            System.Console.WriteLine(cpub);

            using (var streamWriter = new StreamWriter(request.GetRequestStream()))
            {
                string json = "[" + "{" + "\"Ordinal\"" + ":" + $" {order}" + "," + "\"Value\"" + ":" + " 1" + "}" + "]";

                System.Console.WriteLine(json);
                streamWriter.Write(json);
                streamWriter.Flush();
            }
            System.Console.WriteLine(fullUrl);
            
            Task<WebResponse> task = Task.Factory.FromAsync(
                request.BeginGetResponse,
                asyncResult => request.EndGetResponse(asyncResult),
                (object)null);
           
            return task.ContinueWith(t => ReadStreamFromResponse(t.Result)); 
        }

        public static Task<string> getFromTM(string url, string user, string password, string cube, string view)
        {
            string fullUrl = $"{url}/Cubes('{cube}')/Views('{view}')/tm1.Execute?$expand=Cells";

            var userBase64 = Base64Encode($"{user}:");

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(new Uri(fullUrl));
            request.Method = "POST";
            request.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
            request.Headers.Add("Authorization", $"Basic {userBase64}");
            request.ContentType = "application/json";
            using (var streamWriter = new StreamWriter(request.GetRequestStream()))
            {
                string json = "{}";

                streamWriter.Write(json);
                streamWriter.Flush();
            }

            Task<WebResponse> task = Task.Factory.FromAsync(
                request.BeginGetResponse,
                asyncResult => request.EndGetResponse(asyncResult),
                (object)null);

            return task.ContinueWith(t => ReadStreamFromResponse(t.Result));
        }



        public static Task<string> sendToMaximo(string maxURL, string user, string password, string workOrderID, string status)
        {

            string fullUrl = $"{maxURL}/{workOrderID}?_lid={user}&_lpwd={password}&STATUS={status}";
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(new Uri(fullUrl));
            request.Method = "POST";
            request.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
            request.ContentType = "application/json";
            /* using (var streamWriter = new StreamWriter(request.GetRequestStream()))
            {
                string json = "{}";

                streamWriter.Write(json);
                streamWriter.Flush();
            }*/

            Task<WebResponse> task = Task.Factory.FromAsync(
                request.BeginGetResponse,
                asyncResult => request.EndGetResponse(asyncResult),
                (object)null);

            return task.ContinueWith(t => ReadStreamFromResponse(t.Result));
        }

        internal static RemoteCertificateValidationCallback AcceptAllCertifications()
        {
            throw new NotImplementedException();
        }
    }

    public class Options
    {

        [Option('l', "urlTM", Required = true, HelpText = "Your TM1 Address with port, like 'sr-dc-009:10001/api/v1'")]
        public string urlTM1 { get; set; }

        [Option('u', "usernameTM", Required = true, HelpText = "Your TM1 Username")]
        public string userNameTM1 { get; set; }

        [Option('p', "passwordTM", Required = false, HelpText = "Your TM1 Password")]
        public string passwordTM1 { get; set; }

        [Option('c', "tmCube", Required = true, HelpText = "Cube in TM1 DataFrame.")]
        public string cubeTM1 { get; set; }

        [Option('w', "cubeView", Required = true, HelpText = "View in TM1 seleced cube.")]
        public string viewTM1 { get; set; }

        [Option('n', "usernameMaximo", Required = true, HelpText = "Username for Maximo.")]
        public string usernameMaximo { get; set; }

        [Option('x', "passwordMaximo", Required = true, HelpText = "Username for Maximo.")]
        public string passwordMaximo { get; set; }

        [Option('o', "urlMaximo", Required = true, HelpText = "URL to web-service Maximo.")]
        public string maximoUrl { get; set; }

        [Option('v', "verbose", Required = false, HelpText = "Debug + Logs")]
        public bool DebugOn { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this,
            (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }

    public class Cell
    {
        public int Ordinal { get; set; }
        public string Value { get; set; }
        public string FormattedValue { get; set; }

        public Cell() { }
    }

    public class Data
    {

        public string odata { get; set; }
        public string ID { get; set; }
        public List<Cell> Cells { get; set; }

        public Data() { }

    }
}
