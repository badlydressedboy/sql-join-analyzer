using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Diagnostics;
using System.Data.SqlClient;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace SqlJoinAnalyser
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
       
        ObservableCollection<JoinInfo> JoinedTables = new ObservableCollection<JoinInfo>();
        BackgroundWorker bgWorker = new BackgroundWorker();
        string sql;
        SqlConnection _conn;

        public MainWindow()
        {
            InitializeComponent();
            Grid.ItemsSource = JoinedTables;
            bgWorker.DoWork += RunAnalysis;
            Server.Text = Properties.Settings.Default.Server;
            Database.Text = Properties.Settings.Default.Database;
            if (Properties.Settings.Default.LastQuery.ToUpper().Contains("SELECT"))
            {
                InputSqlTextBox.AppendText(Properties.Settings.Default.LastQuery);
            }
            else
            {
                InputSqlTextBox.AppendText("Paste your SQL query here");
            }
            InputSqlTextBox.SetValue(Paragraph.LineHeightProperty, 10.0);
        }

        private void Analyse_Click(object sender, RoutedEventArgs e)
        {
            if (AnalyseButton.Content.ToString() == "Analyse")
            {
                AnalyseButton.Content = "Cancel";                

                string text =
                    new TextRange(InputSqlTextBox.Document.ContentStart, InputSqlTextBox.Document.ContentEnd).Text;
                sql = RemoveControlCharacters(text);

                Properties.Settings.Default.Server = Server.Text;
                Properties.Settings.Default.Database = Database.Text;
                Properties.Settings.Default.LastQuery =
                    new TextRange(InputSqlTextBox.Document.ContentStart, InputSqlTextBox.Document.ContentEnd).Text;
                Properties.Settings.Default.Save();

                JoinedTables.Clear();

                _conn =
                    new SqlConnection("server=" + Server.Text + ";Database=" + Database.Text +
                                      ";Trusted_Connection=True;Connection Timeout=2");

                if (bgWorker.IsBusy != true)
                {                    
                    bgWorker.RunWorkerAsync();
                }
            }
            else
            {
                AnalyseButton.Content = "Analyse";

            }
        }

        private void Capitalize(ref string str, string word)
        {
            str = str.Replace(word, word.ToUpper());            
        }
        private void RunAnalysis(object sender, DoWorkEventArgs e)
        {
            string sqlStatement = "";

            try
            {                                
                int tableCount = 0;                                                

                //get rid of multiple whitespaces
                string temp = System.Text.RegularExpressions.Regex.Replace(sql, @"\s+", " ");                
                
                Capitalize(ref temp," join ");
                Capitalize(ref temp, " from ");
                Capitalize(ref temp, " where ");
                Capitalize(ref temp, " order by ");
                Capitalize(ref temp, " left ");
                Capitalize(ref temp, " right ");
                Capitalize(ref temp, " inner ");
                Capitalize(ref temp, " on ");
                Capitalize(ref temp, " full ");
                Capitalize(ref temp, " outer ");
                Capitalize(ref temp, " cross ");

                temp = temp.Substring(temp.IndexOf("FROM") + 4);
                temp = temp.TrimStart();

                // split on joins
                string[] delimeter = new string[] { "JOIN" };
                string[] joins = temp.Split(delimeter, StringSplitOptions.None);
           
                string savedJoinType = "";
                foreach (string join in joins)
                {

                    string j = join.TrimStart();
                    //Debug.WriteLine("j: " + j);

                    // table inc possible alias
                    temp = j.Substring(j.IndexOf(" ") + 1);
                    temp = temp.Substring(0, temp.IndexOf(" "));                    
                    string table = j.Substring(0, j.IndexOf(" ")) + " " + temp;                    

                    string joinType;
                    string onClause = "";

                    var tableDetails = new Dictionary<string, string>();
                    if (tableCount != 0)
                    {
                        onClause = j.Substring(j.IndexOf(" ON ") + 1);
                        // get rid of anything in where or order by clauses
                        if (onClause.Contains(" WHERE ")) { onClause = onClause.Substring(0, onClause.IndexOf(" WHERE ")); }
                        if (onClause.Contains(" ORDER BY ")) { onClause = onClause.Substring(0, onClause.IndexOf(" ORDER BY ")); }
                        if (onClause.Contains(" LEFT ")) { onClause = onClause.Substring(0, onClause.IndexOf(" LEFT ")); }
                        if (onClause.Contains(" RIGHT ")) { onClause = onClause.Substring(0, onClause.IndexOf(" RIGHT ")); }
                        if (onClause.Contains(" INNER ")) { onClause = onClause.Substring(0, onClause.IndexOf(" INNER ")); }
                        if (onClause.Contains(" LEFT OUTER ")) { onClause = onClause.Substring(0, onClause.IndexOf(" LEFT OUTER ")); }
                        if (onClause.Contains(" RIGHT OUTER ")) { onClause = onClause.Substring(0, onClause.IndexOf(" RIGHT OUTER ")); }
                        if (onClause.Contains(" FULL OUTER ")) { onClause = onClause.Substring(0, onClause.IndexOf(" FULL OUTER ")); }
                        if (onClause.Contains(" FULL ")) { onClause = onClause.Substring(0, onClause.IndexOf(" FULL ")); }
                        //Debug.WriteLine("onClause: " + onClause);
                    }

                    if (j.Contains(" LEFT OUTER ")){ joinType = "LEFT OUTER";}
                    else if (j.Contains(" LEFT ")){ joinType = "LEFT";}
                    else if (j.Contains(" RIGHT OUTER ")){ joinType = "RIGHT OUTER";}
                    else if (j.Contains(" RIGHT ")){ joinType = "RIGHT";}
                    else if (j.Contains(" INNER ")){ joinType = "INNER";}
                    else if (j.Contains(" FULL OUTER ")){ joinType = "FULL OUTER";}
                    else if (j.Contains(" FULL ")){ joinType = "FULL";}
                    else if (j.Contains(" OUTER ")){ joinType = "OUTER";}
                    else{ joinType = "";}

                    JoinInfo joinInfo = new JoinInfo() { Table = table, JoinType = savedJoinType, OnClause = onClause, Status = "Detected" };
                    savedJoinType = joinType;
                    
                    tableDetails.Add(table, joinType);

                    this.Dispatcher.BeginInvoke(new Action(() => JoinedTables.Add(joinInfo)));

                    tableCount++;

                    Debug.WriteLine("");
                }

                sqlStatement = "select count (1) from ";
                               
                _conn.Open();
                int count = 0;
                
                // not sure why this is needed but it is
                System.Threading.Thread.Sleep(10);

                foreach (JoinInfo ji in JoinedTables)
                {
                    // conn may be closed by cancel button
                    if (_conn.State == ConnectionState.Open)
                    {
                        if (count != 0)
                        {
                            sqlStatement += ji.JoinType + " JOIN ";
                        }
                        sqlStatement += ji.Table + " ";
                        if (count != 0)
                        {
                            sqlStatement += ji.OnClause + " ";
                        }

                        Debug.WriteLine("sqlStatement: " + sqlStatement);

                        // perform sql query here
                        ji.Status = "Running query";
                        SqlCommand comm = new SqlCommand(sqlStatement, _conn);                        
                        int returned = (int) comm.ExecuteScalar();
                        ji.Status = "Done";
                        ji.Rows = returned;                                                

                        count++;
                    }
                }
                _conn.Close();               
              
            }
            catch (SqlException ex)
            {
                string error = ex.Message + "\n\nWhile executing:\n\n" + sqlStatement;
                MessageBox.Show(error, "ERROR", MessageBoxButton.OK);
            }
            finally
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    AnalyseButton.Content = "Analyse";                    
                }));
            }
        }

        public static string RemoveControlCharacters(string inString)
        {
            if (inString == null) return null;

            StringBuilder newString = new StringBuilder();
            char ch;

            for (int i = 0; i < inString.Length; i++)
            {

                ch = inString[i];

                if (!char.IsControl(ch))
                {
                    newString.Append(ch);
                }
                else
                {
                    newString.Append(" ");
                }
            }
            return newString.ToString();
        }
    }

    class JoinInfo : INotifyPropertyChanged
    {
        public string Table {get; set;}
        public string JoinType { get; set; }
        public string OnClause { get; set; }
        public string status { get; set; }
        public int rows { get; set; }

        public string Status
        {
            get { return this.status; }

            set
            {
                if (value != this.status)
                {
                    this.status = value;
                    NotifyPropertyChanged("Status");
                }
            }
        }

        public int Rows
        {
            get { return this.rows; }

            set
            {
                if (value != this.rows)
                {
                    this.rows = value;
                    NotifyPropertyChanged("Rows");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(info));
            }
        }
    }
}
