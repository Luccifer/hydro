using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.IO;


namespace GUI_TM1_Maximo
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void buttonClicked(object sender, EventArgs e)
        {
            string user = textBox1.Text;
            string password = textBox2.Text;
            string workOrderID = textBox3.Text;
            string status = textBox4.Text;

            sendToMaxima(user, password, workOrderID, status);

        }

        private async Task<bool> sendToMaxima(string user, string password, string workOrderID, string status)
        {
            string url = $"http://10.1.3.8/maxrest/rest/os/mxwo/{workOrderID}?_lid={user}&_lpwd={password}&STATUS={status}";
            Console.WriteLine(url);
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(new Uri(url));

            request.Method = "POST";
            using (WebResponse response = await request.GetResponseAsync())
            {
                Console.WriteLine(request);
                using (Stream stream = response.GetResponseStream())
                {
                    return true;
                    //process the response
                }
            }
        }
    }

}
