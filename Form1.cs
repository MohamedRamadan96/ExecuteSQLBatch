using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Excel = Microsoft.Office.Interop.Excel;
using OfficeOpenXml;
namespace ExecuteSQLBatch
{
    public partial class Form1 : Form
    {
        private string folderPath;
        private HashSet<string> executedScripts = new HashSet<string>();
        private string clinicCenterName;
        public Form1()
        {
            InitializeComponent();
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (var folderBrowserDialog = new FolderBrowserDialog())
            {
                if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
                {
                    folderPath = folderBrowserDialog.SelectedPath;
                    txtFolderPath.Text = folderPath;
                }
            }
        }
        private void btnExecute_Click(object sender, EventArgs e)
        {
            string serverName = txtServerName.Text;
            string databaseName = txtDatabaseName.Text;
            string userName = txtUsername.Text;
            string password = txtPassword.Text;

            string connectionString = $"Data Source={serverName};Initial Catalog={databaseName};User ID={userName};Password={password};";
            SqlConnection connection = new SqlConnection(connectionString);
            int successCount = 0;
            int errorCount = 0;
            try
            {
                if (databaseName.Equals("ClinicPro", StringComparison.OrdinalIgnoreCase))
                {
                    clinicCenterName = GetClinicCenterName(connection);

                    if (!string.IsNullOrEmpty(clinicCenterName))
                    {
                        this.Text = $"ClinicPro - {clinicCenterName}";
                        MessageBox.Show($"Connection Success with ({clinicCenterName}).", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("Connection Success, but Clinic Center Name not found", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
                else
                {
                    MessageBox.Show("Connection Success", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                string[] scriptFiles = Directory.GetFiles(folderPath, "*.sql", SearchOption.AllDirectories);

                dataGridViewResults.Rows.Clear(); // Clear existing rows in the DataGridView
                executedScripts.Clear(); // Clear the set of executed script names
                prg.Minimum = 0;
                prg.Maximum = scriptFiles.Length;
                prg.Value = 0;
                foreach (string scriptFile in scriptFiles)
                {
                    int rowIndex = dataGridViewResults.Rows.Add();
                    DataGridViewRow row = dataGridViewResults.Rows[rowIndex];
                    row.Cells["ColumnName"].Value = Path.GetFileNameWithoutExtension(scriptFile);
                    row.Cells["ColumnFolder"].Value = Path.GetDirectoryName(scriptFile);

                    string scriptName = Path.GetFileNameWithoutExtension(scriptFile);

                    if (!string.IsNullOrEmpty(scriptFile) && File.Exists(scriptFile) && !executedScripts.Contains(scriptName))
                    {
                        string script = File.ReadAllText(scriptFile);
                        string[] batches = script.Split(new[] { "GO" }, StringSplitOptions.RemoveEmptyEntries);

                        StringBuilder executionResult = new StringBuilder();
                        bool hasError = false;
                        prg.Value++;
                        prg.Update();
                        foreach (string batch in batches)
                        {
                            try
                            {
                                connection.Open();
                                using (SqlCommand command = new SqlCommand(batch, connection))
                                {
                                    command.ExecuteNonQuery();
                                    executionResult.AppendLine("Executed Successfully  |  ");
                                }
                            }
                            catch (Exception ex)
                            {
                                hasError = true;
                                executionResult.AppendLine($"Error: {ex.Message} |");
                                break; // Stop executing remaining batches after encountering an error
                            }
                            finally
                            {
                                connection.Close();
                            }
                        }
                        if (hasError)
                        {
                            errorCount++;
                        }
                        else
                        {
                            successCount++;
                        }

                        row.Cells["ColumnStatus"].Value = hasError ? "Error" : "Success";
                        row.Cells["ColumnResult"].Value = executionResult.ToString();

                        // Set row color based on execution status
                        row.DefaultCellStyle.BackColor = hasError ? Color.LightCoral : Color.LightGreen;

                        executedScripts.Add(scriptName); // Add the executed script name to the set
                    }
                    else if (executedScripts.Contains(scriptName))
                    {
                        row.Cells["ColumnStatus"].Value = "Skipped";
                        row.Cells["ColumnResult"].Value = "Script already executed in a different folder.";
                        row.DefaultCellStyle.BackColor = Color.LightGray;
                    }
                    else
                    {
                        row.Cells["ColumnStatus"].Value = "Error";
                        row.Cells["ColumnResult"].Value = $"Invalid script file path - '{scriptFile}'.";
                        row.DefaultCellStyle.BackColor = Color.LightCoral; // Set row color for invalid script
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("An error occurred: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                connection.Close();
            }

            MessageBox.Show($"Execution Finished\nError Count: {errorCount}\nSuccess Count: {successCount}", "Execution Result", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }


        private string GetClinicCenterName(SqlConnection connection)
        {
            try
            {
                string query = "SELECT ClinicCenter FROM ClinicCenterLogo";
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    connection.Open();
                    object result = command.ExecuteScalar();
                    return result != null ? result.ToString() : null;
                }
            }
            catch (Exception)
            {
                // Handle the exception, if any.
                return null;
            }
            finally
            {
                connection.Close();
            }
        }

        private void dataGridViewResults_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == dataGridViewResults.Columns["ColumnStatus"].Index)
            {
                string status = e.Value?.ToString();

                if (status == "Success")
                {
                    e.CellStyle.BackColor = Color.LightGreen;
                }
                else if (status == "Error")
                {
                    e.CellStyle.BackColor = Color.LightCoral;
                }
            }
        }
    }
}
