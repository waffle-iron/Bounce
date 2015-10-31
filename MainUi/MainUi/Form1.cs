﻿using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using CefSharp;
using CefSharp.WinForms;
using System.Diagnostics;

namespace MainUi
{
    public partial class Form1 : Form
    {
        private BlocklyLua lua_control;
        private ChromiumWebBrowser codeBrowser;
        private OutputConsole con;
        private NodeMCU connection;

        public class DialogState
        {
            public DialogResult result;
            public FileDialog dialog;
 
            public void ThreadProcShowDialog()
            {
                result = dialog.ShowDialog();
            }
        }

        public class FileSystem
        {
            private OpenFileDialog openDialog;
            private SaveFileDialog saveDialog;
            public FileSystem(OpenFileDialog openDialog, SaveFileDialog saveDialog)
            {
                this.openDialog = openDialog;
                this.saveDialog = saveDialog;
            }

            /* STAShowDialog takes a FileDialog and shows it on a background STA thread and returns the results.
             * Usage:
             *   OpenFileDialog d = new OpenFileDialog();
             *   DialogResult ret = STAShowDialog(d);
             *   if (ret == DialogResult.OK)
             *      MessageBox.Show(d.FileName);
             */
            private DialogResult STAShowDialog(FileDialog dialog)
            {
                DialogState state = new DialogState();
                state.dialog = dialog;
                System.Threading.Thread t = new System.Threading.Thread(state.ThreadProcShowDialog);
                t.SetApartmentState(System.Threading.ApartmentState.STA);
                t.Start();
                t.Join();
                return state.result;
            }

            public string LoadFile()
            {
                string data;
                if (STAShowDialog(openDialog) == DialogResult.OK)
                {
                    using (StreamReader reader = new StreamReader(openDialog.OpenFile()))
                    {
                        data = reader.ReadToEnd();
                    }
                }
                else
                {
                    data = "not loaded";
                }
                return data;
            }

            public void SaveFile(string data) { throw new NotImplementedException(); }
        }

        public Form1()
        {
            InitializeComponent();
            Cef.Initialize();

            codeBrowser = new ChromiumWebBrowser(BlocklyLua.GetAddress().ToString());
            codeBrowser.RegisterJsObject("FileSystem", new FileSystem(openFileDialog1, saveFileDialog1));
            Debug.WriteLine(BlocklyLua.GetAddress());
            codeBrowser.Dock = DockStyle.Fill;
            this.splitContainer1.Panel1.Controls.Add(codeBrowser);
            codeBrowser.Location = new Point(0, 0);
            codeBrowser.MinimumSize = new Size(20, 20);
            codeBrowser.Size = new Size(690, 571);
            codeBrowser.IsBrowserInitializedChanged += CodeBrowser_IsBrowserInitializedChanged;

            string appDir = Path.GetDirectoryName(Application.ExecutablePath);
            outputBrowser.Navigate(Path.Combine(appDir, "emptyOutput.html"));
        }

        private void CodeBrowser_IsBrowserInitializedChanged(object sender, IsBrowserInitializedChangedEventArgs e)
        {
            lua_control = new BlocklyLua(codeBrowser);
        }

        private void connectButton_Click(object sender, EventArgs e)
        {
            if (connection == null)
            {
                string node_port = (string)toolStripNodes.SelectedItem;
                connection = new NodeMCU(node_port, con);
                connectButton.Text = "--Connected--";
                connectButton.ToolTipText = "Click to disconnect";
                toolStripNodes.Enabled = false;
            } else
            {
                connection.Close();
                connection = null;
                connectButton.Text = "Connect";
                connectButton.ToolTipText = "Click to connect";
                toolStripNodes.Enabled = true;
            }
        }

        private async void runButton_ButtonClick(object sender, EventArgs e)
        {
            string code;
            runButton.Enabled = false;
            try
            {
                code = await lua_control.GetCode();
            } catch(System.OperationCanceledException err)
            {
                Console.WriteLine("An error has occured reading the code");
                return;
            }
            con.WriteLine(code);
            if (connection != null)
            {
                connection.run_code(code);
            }
            runButton.Enabled = true;
        }

        private void outputBrowser_DocumentCompleted(object sender, WebBrowserDocumentCompletedEventArgs e)
        {
            if(outputBrowser.Url.ToString().Contains("emptyOutput.html")) { 
                con = new HtmlOutputWrapper(outputBrowser.Document);
            }
        }

        private async void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Show the save dialog
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                using (Stream myStream = saveFileDialog1.OpenFile())
                {
                    await lua_control.SaveDocument(myStream);
                }
            }
        }

        private void showWebConsoleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            codeBrowser.ShowDevTools();
        }

        private void loadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            lua_control.InitiateLoad();  
        }

        private void findNodesButton_Click(object sender, EventArgs e)
        {
            var nodes = NodeMCU.find_node(con);
            foreach (var node in nodes)
            {
                toolStripNodes.Items.Add(node.ToString());
            }
        }

        private void toolStripNodes_Click(object sender, EventArgs e)
        {
            connectButton.Enabled = toolStripNodes.SelectedItem != null;
        }

        private void toolStripNodes_OwnerChanged(object sender, EventArgs e)
        {
            connectButton.Enabled = toolStripNodes.SelectedItem != null;
        }
    }
}