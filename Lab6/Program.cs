using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;


namespace Lab6
{
    class Program
    {
        public static string SuccessHeaders(int contentLength)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("HTTP/1.1 200 OK").Append("\r\n");
            builder.Append("Date: ").Append(DateTime.Now).Append("\r\n");
            // builder.Append("Server: Apache").Append("\r\n");
            builder.Append("Content-Type: text/html; charset=UTF-8").Append("\r\n");
            builder.Append("Content-Length: ").Append(contentLength).Append("\r\n");
            builder.Append("Connection: close").Append("\r\n");
            builder.Append("\r\n");

            return builder.ToString();
        }

        private static string AnswerPage(String val)
        {
            StringBuilder bodyBuilder = new StringBuilder();
            bodyBuilder.Append("Answer:");
            bodyBuilder.Append("<br><br>");
            bodyBuilder.Append(val);
            bodyBuilder.Append("<br>");
            bodyBuilder.Append("<br>");
            bodyBuilder.Append("<a href='/'> Home Page </a> ");
            String body = bodyBuilder.ToString();

            return String.Concat(SuccessHeaders(body.Length), body);
        }

        private static string BadAnswerPage()
        {
            StringBuilder bodyBuilder = new StringBuilder();
            bodyBuilder.Append("BadRequest");
            bodyBuilder.Append("<br>");
            bodyBuilder.Append("<a href='/'> Open Index Page </a> ");
            String body = bodyBuilder.ToString();

            return String.Concat(SuccessHeaders(body.Length), body);
        }

        private static string IndexPage()
        {
            StringBuilder bodyBuilder = new StringBuilder();
            bodyBuilder.Append("<form method='post'>");
            bodyBuilder.Append("Enter matrix and vector:");
            bodyBuilder.Append("<br>");
            bodyBuilder.Append("<textarea type='text' name='val' style='width:500px;height:500px;'></textarea>");
            bodyBuilder.Append("<br>");
            bodyBuilder.Append("<input type='submit' >");
            bodyBuilder.Append("</form>");
            String body = bodyBuilder.ToString();

            return String.Concat(SuccessHeaders(body.Length), body);
        }

        static X509Certificate2 serverCertificate = null;

        static void ProcessClient(Object obj)
        {
            TcpClient client = (TcpClient)obj;
            SslStream sslStream = new SslStream(client.GetStream(), false);

            try
            {
                sslStream.AuthenticateAsServer(serverCertificate, false, SslProtocols.Tls12, false);
                sslStream.ReadTimeout = 200;
                sslStream.WriteTimeout = 200;

                Console.WriteLine("Waitingforclientmessage...");
                string messageData = ReadMessage(sslStream);
                Console.WriteLine("request:");
                Console.WriteLine(messageData);
                string page = "";
                if (messageData.StartsWith("POST"))
                {
                    Regex r = new Regex("\r\n\r\n");
                    string[] request = r.Split(messageData, 2);

                    if (request.Length < 1)
                    {
                        page = BadAnswerPage();
                    }
                    else

                    {
                        Regex reg = new Regex("%0D%0A");
                        string val = request[1].Split('=')[1];
                        var newVal = reg.Split(val, 5);
                        string matrix = "";
                        string vector = "";
                        for (int i = 0; i < newVal.Length; i++)
                        {
                            if (i == newVal.Length - 1)
                            {
                                var vecRes = newVal[i].Split('+');
                                for (int h = 0; h < vecRes.Length; h++)
                                {
                                    if (h != vecRes.Length - 1)
                                        vector += vecRes[h] + " ";
                                    else
                                        vector += vecRes[h];
                                }
                            }
                            else
                            {
                                var matrRes = newVal[i].Split('+');
                                for (int h = 0; h < matrRes.Length; h++)
                                {
                                    if (h == matrRes.Length - 1 && i == newVal.Length - 2)
                                        matrix += matrRes[h];
                                    else
                                        matrix += matrRes[h] + " ";
                                }
                            }

                        }
                        string result1 = Resolver.Result(Resolver.ReadMatrix(matrix), Resolver.ReadVector(vector));
                        page = AnswerPage(result1);
                    }
                }
                else
                {
                    page = IndexPage();
                }
                Console.WriteLine("response:");
                Console.WriteLine(page);
                byte[] message = Encoding.UTF8.GetBytes(page);
                sslStream.Write(message, 0, message.Length);
                sslStream.Flush();
            }
            catch (AuthenticationException e)
            {
                Console.WriteLine("Exception:{0}", e.Message);
                if (e.InnerException != null)
                {
                    Console.WriteLine("Innerexception:{0}",
                    e.InnerException.Message);
                }
                Console.WriteLine("Authenticationfailed-closingtheconnection.");
            }
            finally
            {
                sslStream.Close();
                client.Close();
            }
        }

        static void Main(string[] args)
        {
            serverCertificate = new X509Certificate2("output2.cer", "111111");
            TcpListener listener = new TcpListener(IPAddress.Any, 8088);
            listener.Start();
            Console.WriteLine("Server:https://localhost:8088");
            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();
                ThreadPool.QueueUserWorkItem(ProcessClient, client);
            }
        }

        static string ReadMessage(SslStream sslStream)
        {
            StringBuilder messageData = new StringBuilder();
            try
            {
                byte[] buffer = new byte[2048];
                int bytes = -1;
                do
                {
                    bytes = sslStream.Read(buffer, 0, buffer.Length);
                    Decoder decoder = Encoding.UTF8.GetDecoder();
                    char[] chars = new char[decoder.GetCharCount(buffer, 0, bytes)];
                    decoder.GetChars(buffer, 0, bytes, chars, 0);
                    messageData.Append(chars);
                } while (bytes != 0);
            }
            catch (Exception) { }
            return messageData.ToString();
        }
    }
}
