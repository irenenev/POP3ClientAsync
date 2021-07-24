using System;
using System.Collections;

namespace POP3ClientAsync
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                POP3 popMail = new POP3();
                Console.WriteLine("Enter POP3 server name. For example: \"pop.gmail.com\"");
                string server = Console.ReadLine();
                Console.WriteLine("Enter port number. For example: 995");
                int port = int.Parse(Console.ReadLine());
                Console.WriteLine("Enter user name before the sign @");
                string user = Console.ReadLine();
                Console.WriteLine("Enter password");
                string password = Console.ReadLine();
                popMail.ConnectPOP(server, port, user, password);
                /*
                //если нужно получить конкретное письмо по номеру
                POP3EmailMessage msg = new POP3EmailMessage();
                msg.msgNumber = 8;
                POP3EmailMessage POPMsgContent = popMail.RetrieveMessage(msg);
                Console.WriteLine(POPMsgContent.msgHeaders);
                Console.WriteLine(POPMsgContent.msgContent);
                */
                
                //получение всех писем
                ArrayList MessageList = popMail.ListMessages();
                foreach (POP3EmailMessage msg in MessageList)
                {
                    POP3EmailMessage POPMsgContent = popMail.RetrieveMessage(msg);
                    Console.WriteLine("Message {0}:{1}", POPMsgContent.msgNumber, POPMsgContent.msgSize);
                    Console.WriteLine(POPMsgContent.msgHeaders);
                    Console.WriteLine(POPMsgContent.msgContent);
                }
                
                popMail.DisconnectPOP();
                Console.WriteLine("Messages have been received. Close the program");
                Console.ReadLine();
            }
            catch (POPException e)
            {
                Console.WriteLine(e.ToString());
                Console.ReadLine();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Console.ReadLine();
            }
        }
    }
}
