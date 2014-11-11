using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace PocketPlayLabTest
{
    class Program
    {

        #region variables
        
        static readonly Regex RxCountPendingMessages = new Regex(@"/api/users/\d+/count_pending_messages");
        static readonly Regex RxGetMessages = new Regex(@"/api/users/\d+/get_messages");
        static readonly Regex RxGetFriendsProgress = new Regex(@"/api/users/\d+/get_friends_progress");
        static readonly Regex RxGetFriendsScore = new Regex(@"/api/users/\d+/get_friends_score");
        static readonly Regex RxUser = new Regex(@"/api/users/\d+");

        /// <summary>
        /// store unique links with counter of repeats, response time..
        /// </summary>
        static DataTable dtLinks = new DataTable();

        /// <summary>
        /// store links dyno with count of repeats..
        /// </summary>
        static DataTable dtLinkDyno = new DataTable();

        #endregion

        static void Main(string[] args)
        {
            InitialDataTables();
            bool dataReaded = ReadLogData();

            if (dataReaded)
                PrintStatistic();
        }

        private static void InitialDataTables()
        {
            DataColumn cId = new DataColumn("LinkId", typeof(int));
            cId.AutoIncrement = true;
            dtLinks.Columns.Add(cId);
            cId.AutoIncrementSeed = 1;
            dtLinks.Columns.Add("Link", typeof(string));
            DataColumn cCount = new DataColumn("Count", typeof(int));
            cCount.DefaultValue = 0;
            dtLinks.Columns.Add(cCount);
            DataColumn cTime = new DataColumn("ResponseTime", typeof(int));
            cTime.DefaultValue = 0;
            dtLinks.Columns.Add(cTime);

            DataColumn cId2 = new DataColumn("Id", typeof(int));
            cId2.AutoIncrement = true;
            cId2.AutoIncrementSeed = 1;
            dtLinkDyno.Columns.Add(cId2);
            dtLinkDyno.Columns.Add("LinkId", typeof(int));
            dtLinkDyno.Columns.Add("Dyno", typeof(string));

            DataColumn cDynoCount = new DataColumn("Count", typeof(int));
            cDynoCount.DefaultValue = 0;
            dtLinkDyno.Columns.Add(cDynoCount);
        }


        static bool ReadLogData()
        {
            try
            {
                StreamReader reader = new StreamReader("sample.log");

                while (!reader.EndOfStream)
                {
                    var readLine = reader.ReadLine();
                    if (readLine != null)
                    {
                        string[] parsedLine = readLine.Split(' ');

                        CheckLinks(parsedLine);
                    }
                }
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("Log file not found");
                return false;
            }

            return true;
        }

        /// <summary>
        /// match each link with the regex and calculate repeats count
        /// </summary>
        /// <param name="parsedLine">one line of the log file</param>
        static void CheckLinks(string[] parsedLine)
        {
            string path = SplitOnEqualSign(parsedLine[4]); // path index is 4
            string key = "";

            #region match link with regex

            if (RxCountPendingMessages.IsMatch(path))
                key = "GET /api/users/{user_id}/count_pending_messages";
            else if (RxGetMessages.IsMatch(path))
                key = "GET /api/users/{user_id}/get_messages";
            else if (RxGetFriendsProgress.IsMatch(path))
                key = "GET /api/users/{user_id}/get_friends_progresss";
            else if (RxGetFriendsScore.IsMatch(path))
                key = "GET /api/users/{user_id}/get_friends_score";
            else if (RxUser.IsMatch(path))
            {
                // this case it may be GET or POST.. check the method
                string method = SplitOnEqualSign(parsedLine[3]); // method index is 3
                if (method == "POST")
                    key = "POST /api/users/{user_id}";
                else
                    key = "GET /api/users/{user_id}";
            }

            #endregion

            // if the key matched any of the regex, count it
            if (!string.IsNullOrEmpty(key))
            {
                int linkId;
                int time = GetResponseTime(parsedLine);
               
                //update the requests count, or add new link
                var row = dtLinks.AsEnumerable().Where(x => x.Field<string>("Link") == key).FirstOrDefault();
                if (row != null)
                {
                    linkId = Convert.ToInt16(row["LinkId"]);
                    row.BeginEdit();
                    row["Count"] = Convert.ToInt32(row["Count"]) + 1;
                    row["ResponseTime"] = Convert.ToInt32(row["ResponseTime"]) + time;
                    row.EndEdit();
                }
                else
                {
                    DataRow newLink = dtLinks.NewRow();
                    newLink["Link"] = key;
                    newLink["Count"] = 1;
                    newLink["ResponseTime"] = time;
                    dtLinks.Rows.Add(newLink);

                    linkId = Convert.ToInt16(newLink["LinkId"]);
                }

                //store dyno for the current link..
                GetLinkDyno(parsedLine: parsedLine, linkId: linkId);
            }
        }

        /// <summary>
        /// check and calculate response time
        /// </summary>
        /// <param name="parsedLine">one line of the log file</param>
        /// <returns>total response time (connect time  + service Time)</returns>
        static int GetResponseTime(string[] parsedLine)
        {
            int connect = 0, serviceTime = 0;
            string connectText = SplitOnEqualSign(parsedLine[8]), serviceTimeText = SplitOnEqualSign(parsedLine[9]); // connect index is 8, service time index is 9

            if (int.TryParse(connectText.Remove(connectText.Length - 2, 2), out connect))
            {
                if (int.TryParse(serviceTimeText.Remove(serviceTimeText.Length - 2, 2), out serviceTime))
                {
                    return (connect + serviceTime);
                }
            }
            return 0;
        }

        /// <summary>
        /// check dyno for spicific link and count it
        /// </summary>
        /// <param name="parsedLine">one line of the log file</param>
        /// <param name="linkId">the link Id that will check for</param>
        static void GetLinkDyno(string[] parsedLine, int linkId)
        {
            string dyno = SplitOnEqualSign(parsedLine[7]); // dyno index is 7

            if (!string.IsNullOrEmpty(dyno))
            {
                //check if that dyno is already there for the linkId or not
                var row = dtLinkDyno.AsEnumerable().Where(x => x.Field<int>("LinkId") == linkId && x.Field<string>("Dyno") == dyno).FirstOrDefault();
                if (row != null)
                {
                    row.BeginEdit();
                    row["Count"] = Convert.ToInt32(row["Count"]) + 1;
                    row.EndEdit();
                }
                else
                {
                    DataRow newLink = dtLinkDyno.NewRow();
                    newLink["Dyno"] = dyno;
                    newLink["Count"] = 1;
                    newLink["LinkId"] = linkId;
                    dtLinkDyno.Rows.Add(newLink);
                }
            }
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
            foreach (DataRow link in dtLinks.Rows)
            {
                // get top dyno
                var topDyno = dtLinkDyno.AsEnumerable()
                                        .Where(x => x.Field<int>("LinkId") == Convert.ToInt32(link["LinkId"]))
                                        .OrderByDescending(o => o.Field<int>("Count")).FirstOrDefault();

                decimal avgTime = Math.Round((Convert.ToInt32(link["ResponseTime"]) / Convert.ToDecimal(link["Count"])),2);
                string dyno = "-";
                if (topDyno != null)
                    dyno = string.Format("{0} ({1} times)", topDyno["Dyno"], topDyno["Count"]);


                Console.WriteLine(string.Format("Link: {1}       Requests Count: {0}     Avg. Response Time: {2} ms     Most Dyno: {3}",
                    link["Count"], link["Link"], avgTime, dyno));
            }
        }


    }
}
