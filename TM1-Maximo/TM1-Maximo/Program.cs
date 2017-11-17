using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Xml.Serialization;
using System.Net;
using CommandLine;
using CommandLine.Text;

namespace TM1_Maximo
{
    class Program
    {
        static void Main(string[] args)
        {
            API getXMLs;
            var options = new Options();

            if (Parser.Default.ParseArguments(args, options))
            {
                string fileNameCSV = options.InputFile;
                string fileNameSettings = options.SettingsFile;

                try
                {
                    var processor = new Processor(options);

                    Console.WriteLine("Begin Processing");

                    processor.DoProcess();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error:" + e.Message + e.StackTrace);
                }

                Console.WriteLine("End Processing");
            }
        }
    }

    class Processor
    {
        private string fileNameCSV;
        private string fileNameSettings;
        private string url;
        private string userName;
        private string userPassword;

        private Options options;

        public Processor(Options options)
        {
            this.fileNameCSV = options.InputFile;
            this.fileNameSettings = options.SettingsFile;
            this.url = options.URL;
            this.userName = options.UserName;
            this.userPassword = options.Password;
 
            this.options = options;
        }

        public void DoProcess()
        {
           
            var data = ReadCSV2List(fileNameCSV);
            var settings = ReadSettings2List(fileNameSettings);

            DoProcessFile(data, settings);
        }

        private void DoProcessFile(List<string[]> data, List<string[]> settings)
        {
            if (!CheckData(data) || !CheckSettings(settings))
                return;

            var h = data[0].ToList();

            int dataRowCount = data.Count() - 1;

            var binding = new CustomBinding("Z_CognosIN_soap12");

            var epa = new EndpointAddress(url);

            //SAPService.Z_CognosINClient sapClient = new SAPService.Z_CognosINClient(binding, epa);

            //sapClient.ClientCredentials.UserName.UserName = userName;
            //sapClient.ClientCredentials.UserName.Password = password;

            //maxClient.userName = userName;
            //maxClient.userPassword = password;

            for (int i = 1; i <= dataRowCount; i++)
            {
                SapService.ZPPM_COGNOS_IN_FIN_PLAN sapPlan = new SapService.ZPPM_COGNOS_IN_FIN_PLAN();

                SapService.ZPPMS_COGNOS_IMPORT sapImport = new SapService.ZPPMS_COGNOS_IMPORT();

                #region заполняем поля сап структуры

                foreach (var s in settings)
                {
                    int idx = h.IndexOf(s[0]);
                    if (idx < 0)
                        throw new Exception(String.Format("Error: field {0} not found in csv file", s[0]));

                    var val = data[i][idx];
                    var fieldName = s[1];

                    try
                    {
                        var pi = sapImport.GetType().GetProperty(fieldName);

                        pi.SetValue(sapImport, Cast2FieldType(pi, val), null);
                    }
                    catch
                    {
                        Console.WriteLine(String.Format("Unable to set SAP field: name '{0}' value '{1}', csv row '{2}'", fieldName, val, i));
                    }

                    sapPlan.IS_PARAMS = sapImport;
                }

                #endregion

                SapService.ZPPM_COGNOS_IN_FIN_PLANRequest sapRequest = new SapService.ZPPM_COGNOS_IN_FIN_PLANRequest();

                sapRequest.ZPPM_COGNOS_IN_FIN_PLAN = sapPlan;

                
                
                    try
                    {
                        string filePath = Path.Combine(Path.GetDirectoryName(options.InputFile), Path.GetFileNameWithoutExtension(options.InputFile) + String.Format("_{0}.xml", i));

                        XmlSerializer xmlSer = new XmlSerializer(typeof(SapService.ZPPM_COGNOS_IN_FIN_PLANRequest));
                        FileStream fsXml = new FileStream(filePath, FileMode.Create);
                        xmlSer.Serialize(fsXml, sapRequest);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(String.Format("Row {0}  - error write to XML file - '{1}'", i, e.Message));
                    }

                
                
                /*{
                    try
                    {
                        //SAPService.ZPPM_COGNOS_IN_FIN_PLANResponse sapRespone = sapClient.ZPPM_COGNOS_IN_FIN_PLAN(sapPlan);
                        if (sapRespone.ES_RESULT.ERROR == 0)
                        {
                            logger.Log(String.Format("Row {0}  - SAP response - '{1} {2}'", i, sapRespone.ES_RESULT.ERROR, sapRespone.ES_RESULT.TEXT));
                        }
                        else
                        {
                            logger.Error(String.Format("Row {0}  - SAP response - '{1} {2}'", i, sapRespone.ES_RESULT.ERROR, sapRespone.ES_RESULT.TEXT));
                        }
                    }
                    catch (Exception e)
                    {
                        logger.Error(String.Format("Row {0}  - webservice - '{1} {2}'", i, e.Message, e.InnerException));
                    }
                }*/

            }
        }

        private object Cast2FieldType(PropertyInfo propertyInfo, string value)
        {
            object ret = null;

            switch (propertyInfo.PropertyType.Name)
            {
                case "Decimal":
                    ret = Decimal.Parse(value);
                    break;

                default:
                    ret = value;
                    break;
            }

            return ret;
        }

        private bool CheckSettings(List<string[]> settings)
        {
            //ToDo: crate this
            return true;
        }

        private bool CheckData(List<string[]> data)
        {
            //ToDo: crate this
            return true;
        }

        private List<string[]> ReadCSV2List(string fileNameCSV, Encoding encoding = null)
        {
            if (encoding == null)
                encoding = Encoding.GetEncoding(1251);

            List<string[]> ret = new List<string[]>();

            var reader = new StreamReader(File.OpenRead(fileNameCSV), encoding);

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                var values = line.Split('|');

                ret.Add(values);
            }

            return ret;
        }

        private List<string[]> ReadSettings2List(string fileSettings, Encoding encoding = null)
        {
            if (encoding == null)
                encoding = Encoding.GetEncoding(1251);

            List<string[]> ret = new List<string[]>();

            var reader = new StreamReader(File.OpenRead(fileSettings), encoding);

            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                var values = line.Split('|');

                ret.Add(values);
            }

            return ret;
        }

    }

    class Options
    {
        [Option('f', "file", Required = true, HelpText = "Filename wit data (csv file).")]
        public string InputFile { get; set; }

        [Option('s', "settings", Required = true, HelpText = "File with settings (column mapping)")]
        public string SettingsFile { get; set; }

        [Option('u', "url", Required = true, HelpText = "URL to web-service Maximo.")]
        public string URL { get; set; }

        [Option('n', "name", Required = true, HelpText = "User name to service Maximo.")]
        public string UserName { get; set; }

        [Option('p', "password", Required = true, HelpText = "Password to service maximo.")]
        public string Password { get; set; }

        [Option('d', "debug", Required = false, HelpText = "Only debug. Create XML files with data. Not send to service.")]
        public bool DebugOnly { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this,
            (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }

    class API
    {
        public async Task<bool> getXMLs ()
        {
            string url = "http://10.1.3.8/maxrest.rest.os.mxwo/851000083379/?_lid=maxadmin&_lpwd=mjnhbg543&STATUS=%D0%A7%D0%95%D0%A0%D0%9d%D0%9E%D0%92%D0%98%D0%9A&_format=json";
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(new Uri(url));
            request.Method = "POST";
            using (WebResponse response = await request.GetResponseAsync ())
            {
                using (Stream stream = response.GetResponseStream ())
                {
                    return true;
                        // response here
                }
            }

        }

    }
}
