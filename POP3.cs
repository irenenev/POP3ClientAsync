using System;
using System.Text;
using System.Net.Sockets;
using System.Collections;
using System.Net.Security;
using System.Security.Authentication;
using System.Threading.Tasks;

namespace POP3ClientAsync
{
    public class POP3 : System.Net.Sockets.TcpClient
    {
        //сообщение
        private string Message;
        //результирующий ответ сервера
        private string Result;
        //remote endpoint to establish a socket connection
        private TcpClient tcpClient = new TcpClient();
        //SslStream - поток для обмена данными между клиентом и сервером по протоколу безопасности SSL 
        private SslStream ns = null;

        //Метод соединения с сервером
        public void ConnectPOP(string ServerName, int Port, string UserName, string Password)
        {

            tcpClient.Connect(ServerName, Port);
            ns = new SslStream(tcpClient.GetStream(), false); //false - закрытие внутреннего потока
            try
            {
                ns.AuthenticateAsClient(ServerName);
            }
            catch (AuthenticationException e)
            {
                Console.WriteLine("Exception: {0}", e.Message);
                if (e.InnerException != null)
                {
                    Console.WriteLine("Inner exception: {0}", e.InnerException.Message);
                }
                Console.WriteLine("Аутентификация не удалась");
                tcpClient.Close();
            }
            //Получение ответа
            Result = Response().Result;
            //проверка ответа
            if (Result.Substring(0, 3) != "+OK")
                throw new POPException(Result);
            //отправка имени пользователя
            Message = "USER " + UserName + "\r\n";
            //Метод Write() отправляет данные через Tcp соединение
            Write(Message);
            Result = Response().Result;
            //проверка ответа
            if (Result.Substring(0, 3) != "+OK")
                throw new POPException(Result);
            //отправка пароля
            Message = "PASS " + Password + "\r\n";
            Write(Message);
            Result = Response().Result;
            //проверка ответа
            if (Result.Substring(0, 3) != "+OK")
                throw new POPException(Result);
        }

        //Метод получения ответа сервера
        private async Task<string> Response()
        {
            //кодировка UTF8
            UTF8Encoding EncodedData = new UTF8Encoding();
            //буфер ответа сервера размером 2^10
            byte[] ServerBuffer = new Byte[1024];
            //количество байт ответа сервера
            int count = 0;
            //считывание данных из сетевого потока в буфер
            while (true)
            {
                byte[] buff = new Byte[2];
                //считываем из сетевого потока 1 байт, записываем в buff
                int bytes = await ns.ReadAsync(buff, 0, 1);
                if (bytes == 1) // если удалось получить 1 байт
                {
                    ServerBuffer[count] = buff[0];
                    count++;
                    if (buff[0] == '\n') break; //конец строки
                }
                else break; //нет данныхдля чтения
            }
            //декодирование из массива байт в UTF8-строку
            string ReturnValue = EncodedData.GetString(ServerBuffer, 0, count);
            return ReturnValue;
        }

        //Метод отправляет данные через Tcp соединение
        private void Write(string Message)
        {
            //кодировка UTF8
            UTF8Encoding EncodedData = new UTF8Encoding();
            //буфер для отправки на сервер размером 2^10
            byte[] WriteBuffer = new Byte[1024];
            //помещение кодированного сообщения в буфер
            WriteBuffer = EncodedData.GetBytes(Message);
            //вывод содержимого буфера в поток Tcp
            ns.Write(WriteBuffer, 0, WriteBuffer.Length);
        }

        //Метод получения конкретного сообщения
        public POP3EmailMessage RetrieveMessage(POP3EmailMessage msg)
        {
            POP3EmailMessage MailMessage = new POP3EmailMessage();
            //объект StringBuilder для сохранения нескольких строк ответа
            StringBuilder ResponseStr = new StringBuilder();
            MailMessage.msgSize = msg.msgSize;
            MailMessage.msgNumber = msg.msgNumber;
            //команда RETR для получения сообщения
            Message = "RETR " + msg.msgNumber + "\r\n";
            Write(Message);
            Result = Response().Result;
            //проверка ответа
            if (Result.Substring(0, 3) != "+OK")
                throw new POPException(Result);
            ResponseStr.Append(Result);
            //сообщение получено, установка флага прочтения в true
            MailMessage.msgReceived = true;
            //получение тела сообщения, пока не встретится конец "."
            while (true)
            {
                Result = Response().Result;
                if (Result == ".\r\n") break;
                ResponseStr.Append(Result);
            }
            //строка ответа содержит всё сообщение
            Result = ResponseStr.ToString();
            //поиск позиции конца заголовка
            int headerPos = Result.IndexOf("\r\n\r\n");
            // Если хвост не найден, значит в теле сообщения только заголовки
            if (headerPos == -1)
                MailMessage.msgHeaders = Result;
            // Если хвост найден, отделяем заголовки
            else
            {
                MailMessage.msgHeaders = Result.Substring(0, headerPos);
                MailMessage.msgContent = Result.Substring(headerPos + 4, Result.Length - headerPos - 4);
            }
            //parse
            MailMessage.ParseMail();
            return MailMessage;
        }

        //Метод получения списка сообщений
        public ArrayList ListMessages()
        {
            ArrayList returnArray = new ArrayList();
            Message = "LIST\r\n";
            Write(Message);
            Result = Response().Result;
            //Console.WriteLine("LIST " + Result);
            //проверка ответа
            if (Result.Substring(0, 3) != "+OK")
                throw new POPException(Result);
            while (true)
            {
                Result = Response().Result;
                if (Result == ".\r\n") return returnArray;
                else
                {
                    POP3EmailMessage MailMessage = new POP3EmailMessage();
                    //определение разделителя
                    char[] separator = { ' ' };
                    //разбиение массива данных
                    string[] values = Result.Split(separator);
                    //помещение данных в объект MailMessage
                    MailMessage.msgNumber = Int32.Parse(values[0]);
                    MailMessage.msgSize = Int32.Parse(values[1]);
                    MailMessage.msgReceived = false;
                    returnArray.Add(MailMessage);
                    continue;
                }
            }
        }

        //Метод удаления сообщения
        public void DeleteMessage(POP3EmailMessage msg)
        {
            Message = "DELE " + msg.msgNumber + "\r\n";
            Write(Message);
            Result = Response().Result;
            //проверка ответа
            if (Result.Substring(0, 3) != "+OK")
                throw new POPException(Result);
        }

        //Метод отсоединения от сервера
        public void DisconnectPOP()
        {
            Message = "QUIT\r\n";
            Write(Message);
            Result = Response().Result;
            //проверка ответа
            if (Result.Substring(0, 3) != "+OK")
                throw new POPException(Result);
            ns.Close();
        }
    }
}
