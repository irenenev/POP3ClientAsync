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
                int Number = popMail.StatMessages();
                Console.WriteLine("Your mailbox has this number of messages: " + Number);
                Console.WriteLine(" Receive all messages-<A>\n Receive one message by number-<N>\n");
                ConsoleKeyInfo cki = Console.ReadKey(true);
                POP3EmailMessage msg, POPMsgContent;
                ArrayList MessageList;
                switch (cki.Key.ToString())
                {
                    case "A":
                        //получение всех писем
                        MessageList = popMail.ListMessages();
                        foreach (POP3EmailMessage msge in MessageList)
                        {
                            POPMsgContent = popMail.RetrieveMessage(msge);
                            Console.WriteLine("Message {0}:{1}", POPMsgContent.msgNumber, POPMsgContent.msgSize);
                            Console.WriteLine(POPMsgContent.msgHeaders);
                            Console.WriteLine(POPMsgContent.msgContent);
                        }
                        break;
                    case "N":
                        Console.WriteLine("Enter number of message");
                        int msgNum = Int32.Parse(Console.ReadLine());
                        if (msgNum > Number || msgNum <= 0)
                        {
                            Console.WriteLine("Wrong number");
                            break;
                        }
                        //если нужно получить конкретное письмо по номеру
                        msg = new POP3EmailMessage();
                        msg.msgNumber = msgNum;
                        POPMsgContent = popMail.RetrieveMessage(msg);
                        Console.WriteLine(POPMsgContent.msgHeaders);
                        Console.WriteLine(POPMsgContent.msgContent);
                        break;
                    default:
                        Console.WriteLine("You entered wrong letter. Try again");
                        break;
                }
                popMail.DisconnectPOP();
                Console.WriteLine("Close the program");
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
