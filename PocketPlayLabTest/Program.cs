using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PocketPlayLabTest
{
    class Program
    {

        #region variables

        /// <summary>
        /// store unique links with counter of repeats..
        /// </summary>
        static Hashtable htLinks = new Hashtable();
        /// <summary>
        /// store unique dyno with counter of repeats..
        /// </summary>
        static Dictionary<string, int> dcDyno = new Dictionary<string, int>();

        
        static Regex rxCount_pending_messages = new Regex(@"/api/users/\d+/count_pending_messages");
        static Regex rxGet_messages = new Regex(@"/api/users/\d+/get_messages");
        static Regex rxGet_friends_progress = new Regex(@"/api/users/\d+/get_friends_progress");
        static Regex rxGet_friends_score = new Regex(@"/api/users/\d+/get_friends_score");
        static Regex rxUser = new Regex(@"/api/users/\d+");

        private static int totalResponseTime = 0; //in milleseconds..

        #endregion

        static void Main(string[] args)
        {
            ReadLogData();

            PrintStatistic();
        }
        

        static void ReadLogData()
        {
            StreamReader reader = new StreamReader("sample.log");

            while (!reader.EndOfStream)
            {
                var readLine = reader.ReadLine();
                if (readLine != null)
                {
                    string[] parsedLine = readLine.Split(' ');

                    CheckLinks(parsedLine);

                    CheckDyno(parsedLine);

                    GetResponseTime(parsedLine);
                }
            }
        }

        /// <summary>
        /// match each link with the regex and calculate repeats count
        /// </summary>
        /// <param name="parsedLine">one line of the log file</param>
        static void CheckLinks(string[] parsedLine)
        {
            string path = SplitOnEqualSign(parsedLine[4]); // path index is 4
            string key = "";
            if (rxCount_pending_messages.IsMatch(path))
            {
                key = "GET /api/users/{user_id}/count_pending_messages";
            }
            else if (rxGet_messages.IsMatch(path))
            {
                key = "GET /api/users/{user_id}/get_messages";
            }
            else if (rxGet_friends_progress.IsMatch(path))
            {
                key = "GET /api/users/{user_id}/get_friends_progresss";
            }
            else if (rxGet_friends_score.IsMatch(path))
            {
                key = "GET /api/users/{user_id}/get_friends_score";
            }
            else if (rxUser.IsMatch(path))
            {
                // this case it may be GET or POST.. check the method
                string method = SplitOnEqualSign(parsedLine[3]); // method index is 3
                if (method == "POST")
                    key = "POST /api/users/{user_id}";
                else
                    key = "GET /api/users/{user_id}";
            }

            // if the key matched any of the regex, count it
            if (!string.IsNullOrEmpty(key))
            {
                if (htLinks.ContainsKey(key))
                    htLinks[key] = Convert.ToInt32(htLinks[key]) + 1;
                else
                    htLinks.Add(key, 1);
            }
        }

        /// <summary>
        /// check and calculate response time
        /// </summary>
        /// <param name="parsedLine">one line of the log file</param>
        static void GetResponseTime(string[] parsedLine)
        {
            int connect = 0, serviceTime = 0;
            string connectText = SplitOnEqualSign(parsedLine[8]), serviceTimeText = SplitOnEqualSign(parsedLine[9]); // connect index is 8, service time index is 9

            if (int.TryParse(connectText.Remove(connectText.Length - 2, 2), out connect))
            {
                if (int.TryParse(serviceTimeText.Remove(serviceTimeText.Length - 2, 2), out serviceTime))
                {
                    totalResponseTime += connect + serviceTime;
                }
            }

        }

        /// <summary>
        /// check dyno repeated count
        /// </summary>
        /// <param name="parsedLine">one line of the log file</param>
        static void CheckDyno(string[] parsedLine)
        {
            string dyno = SplitOnEqualSign(parsedLine[7]); // dyno index is 7
            if (dcDyno.ContainsKey(dyno))
                dcDyno[dyno] = Convert.ToInt32(dcDyno[dyno]) + 1;
            else
                dcDyno.Add(dyno, 1);
        }

        /// <summary>
        /// split text on equal sign '=', and return the value after the equal sign
        /// </summary>
        /// <param name="value"></param>
        /// <returns>return the value after the equal sign</returns>
        static string SplitOnEqualSign(string value)
        {
            if (value.Contains("="))
                return value.Split('=')[1];

            return value;
        }

        private static void PrintStatistic()
        {
            foreach (var link in htLinks.Keys)
            {
                Console.WriteLine(string.Format("Count: {0}  Link: {1}", htLinks[link], link));
            }

            Console.WriteLine();
            Console.WriteLine("=============================================");
            Console.WriteLine("=============================================");
            Console.WriteLine();
            Console.WriteLine(string.Format("the average of the response time= {0} ms", (totalResponseTime / htLinks.Count)));
            Console.WriteLine();

            Console.WriteLine("=============================================");
            Console.WriteLine("=============================================");

            var x = dcDyno.OrderByDescending(kp => kp.Value).First();
            Console.WriteLine();
            Console.WriteLine(string.Format("The dyno that responded the most: {0} ({1} time(s))", x.Key, x.Value));
        }
    }
}
