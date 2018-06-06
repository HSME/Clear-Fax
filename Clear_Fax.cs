using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Configuration;


namespace Clear_Fax
{
    public partial class Clear_Fax : Form
    {
        SqlCommand command;
        string sql = null;
        SqlConnection connection;
        List<Group> GroupList;
        List<Del> DelList;
        int i;
        int Error = 0;

        public Clear_Fax()
        {
            {
                InitializeComponent();
                GetConnection();
                Groupcount();
                LoadSetting();
                MainP();
                CloseP();
                Writelog();
            }
        }     

        // Connect to SQL databate
        public void GetConnection()
        {
            string connetionString = Properties.Settings.Default.connectionString;
            connection = new SqlConnection(connetionString);
            try
            { connection.Open(); }
            catch (Exception ex)
            { MessageBox.Show("Can not open connection ! " + ex.Message); }
        }

        // Find the group information
        public void Groupcount()
        {
            GroupList = new List<Group>();
            sql = "select *  from faxgroup";
            command = new SqlCommand(sql, connection);
            SqlDataReader rdr = command.ExecuteReader();
            while (rdr.Read())
            {
                GroupList.Add(new Group()
                {   // Save the data to Group
                    groupname = rdr.GetString(rdr.GetOrdinal("groupname")),
                    keepday = rdr.GetInt16(rdr.GetOrdinal("keepday"))
                });
            }
            rdr.Close();
        }

        // Find the File which over the keepday
        public void LoadSetting()
        {
            DelList = new List<Del>();
            int TotalG = GroupList.Count(); // Total no. of group
            for (int x = 0; x < TotalG; x++)
            {
                sql = "select fg.*,f.*, fg.storelocation+f.filepath as fullname from faxgroup fg left join faxcontent f on fg.groupno = f.groupno  where fg.groupname = @Groupname And (revdatetime < dateadd(day, -fg.keepday, getdate()))";
                command = new SqlCommand(sql, connection);
                command.Parameters.Add("@Groupname", SqlDbType.Char);
                command.Parameters["@Groupname"].Value = GroupList[x].groupname;
                command.Dispose();
                SqlDataReader rdr = command.ExecuteReader();

                if (rdr.Read())
                {
                    DelList.Add(new Del()
                    {   // Save the data to Del
                        groupname = rdr.IsDBNull(rdr.GetOrdinal("groupname")) ? "null" : rdr.GetString(rdr.GetOrdinal("groupname")),
                        fullname = rdr.IsDBNull(rdr.GetOrdinal("fullname")) ? "null" : rdr.GetString(rdr.GetOrdinal("fullname")),
                        revdatetime = rdr.GetDateTime(rdr.GetOrdinal("revdatetime")),
                        faxid = rdr.GetInt32(rdr.GetOrdinal("faxid")),
                        keepday = rdr.GetInt16(rdr.GetOrdinal("keepday"))
                    });
                }
                rdr.Close();
            }
        }

        // Delete the backup on the PC
        public void FileDelete(Del Del)
        {
            if (System.IO.File.Exists(@Del.fullname))  //find the file
            {
                try
                {
                    System.IO.File.Delete(@Del.fullname);  // Delete the file
                    ListBoxPrint.Items.Add("Delete File: " + Del.fullname);
                }
                catch (System.IO.IOException e)
                { ListBoxPrint.Items.Add("Error!" + e.Message); }
            }
            else //No file
            { ListBoxPrint.Items.Add("No file exist."); }
        }

        // Delete the data on SQL databate
        public void SQLDelete(Del Del)
        {
            if (System.IO.File.Exists(@Del.fullname))
            {
                Error++; // Count the error
                ListBoxPrint.Items.Add("Error! File still not delete.");
            }
            else
            {
                ListBoxPrint.Items.Add("Data deleted. Faxid : " + Del.faxid);
                sql = "delete from faxcontent where faxid = @faxid ";
                command = new SqlCommand(sql, connection);
                command.Parameters.Add("@faxid", SqlDbType.Int);
                command.Parameters["@faxid"].Value = Del.faxid;
                command.ExecuteNonQuery();
                command.Dispose();
            }
        }

        // Print the process on window
        public void MainP()
        {
            ListBoxPrint.BeginUpdate();

            foreach (Del ele in DelList)
            {
                ListBoxPrint.Items.Add("Delete file path : " + ele.fullname);
                ListBoxPrint.Items.Add("Delete file faxid : " + ele.faxid);
                ListBoxPrint.Items.Add("Delete file groupname : " + ele.groupname);
                ListBoxPrint.Items.Add("Delete file revdatetime : " + ele.revdatetime);
                FileDelete(ele);
                SQLDelete(ele);
            }
            if (ListBoxPrint.Items.Count == 0)
            { ListBoxPrint.Items.Add("No File Exist Keepday!"); }
            else
            {
                int success = DelList.Count - Error; // Excluded the error
                ListBoxPrint.Items.Add("Deleted " + success + " file" + (success > 1 ? "s." : "."));
            }
            ListBoxPrint.EndUpdate();

        }

        // Close the program
        public void CloseP()
        {
            connection.Close();
            i = 5;  // i = second
            SLabel1.Text = "Done. Window will close in " + i + " second" + (i > 1 ? "s." : ".");
            Timer1.Start();
        }

        // write the log after finish the process
        private void Writelog()
        {
            DateTime now = DateTime.Now;
            string path = PathAddBackslash(Properties.Settings.Default.logpath);  // Can change to anywhere you like in config
            string filename = path + "Clear_Fax_Log_" + now.ToString("yyyyMM") + ".log"; // Set a new log per month
            if (!Directory.Exists(path))  // Create a new directory if need
            { Directory.CreateDirectory(path); }      
            using (StreamWriter sw = new StreamWriter(filename, true)) 
            
                for (int x = 0 ; x<ListBoxPrint.Items.Count;  x++)
            {
                    sw.WriteLine("[ "+now+" ] "+ ListBoxPrint.Items[x].ToString());
            }
        }

        // Run per second
        private void Timer1_Tick(object sender, EventArgs e)
        {
            i--; // -1 second
            SLabel1.Text = "Done. Window will close in " + i + " second" + (i > 1 ? "s." : ".");
            if (i == 0)  // When pass 5 seconds 
            { this.Close(); }
        }

        private string PathAddBackslash(string path)
        {
            // They're always one character but EndsWith is shorter than
            // array style access to last path character. Change this
            // if performance are a (measured) issue.
            string separator1 = "\\";
            string separator2 = "/";

            // Trailing white spaces are always ignored but folders may have
            // leading spaces. It's unusual but it may happen. If it's an issue
            // then just replace TrimEnd() with Trim(). 
            path = path.TrimEnd();

            // Argument is always a directory name then if there is one
            // of allowed separators then I have nothing to do.
            if (path.EndsWith(separator1) || path.EndsWith(separator2))
                return path;

            if (path.Contains(separator1))
                return path + separator1;
            //
            // return path + separator2;
            if (path.Contains(separator2))
                return path + separator2;

            // If there is not an "alt" separator I add a "normal" one.
            // It means path may be with normal one or it has not any separator
            // (for example if it's just a directory name). In this case I
            // default to normal as users expect.
            return path + separator1;
        }

        // Save the groupname and the keepday.
        public class Group
        {
            public string groupname;
            public short keepday;
            public Group()
            {
                groupname = "";
                keepday = 30;
            }
        }

        // The Files which are over the keepday will save the data as Del.
        public class Del
        {
            public string groupname;
            public string fullname;
            public short keepday;
            public DateTimeOffset revdatetime;
            public int faxid;
            public Del()
            {
                groupname = "";
                fullname = "";                
                keepday = 30;
                revdatetime = DateTime.Now;
                faxid = 0;
            }
        }
    }
}





