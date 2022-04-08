using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;

using RouteButler_KUKA;
using System.Threading;
using System.Threading.Tasks;

namespace KUKALibraryTest
{
    public partial class FormRobot : Form
    {
        Kuka kuka = new Kuka();

        bool _busy, _active, _done, _aborted, _error;
        int _errorID;


        public FormRobot()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            tmr_statusupdate.Stop();

        }

        public void button1_Click(object sender, EventArgs e)
        {
            if (button1.Text == "disconnect")
            {
                kuka.Connect(textBox1.Text, textBox12.Text, "1336", "1337");
                tmr_statusupdate.Start();
            }
            else
            {
                kuka.Close();
                tmr_statusupdate.Stop();
                button1.Text = "disconnect";
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            kuka.ResetAlarm();
        }

        KukaRoute kukaRoute;
        private void button3_Click(object sender, EventArgs e)
        {
            kukaRoute = kuka.SaveFile(routeBook);
            kuka.RunProgram(kukaRoute);

        }

        private void button4_Click(object sender, EventArgs e)
        {
            loadroutefile.ShowDialog();
            MessageBox.Show("done");
        }

        private void button5_Click(object sender, EventArgs e)
        {
            savestreamfile.ShowDialog();
            Librarian_KUKA librarian = new Librarian_KUKA();
            librarian.SaveFile(routeBook, savestreamfile.SelectedPath, routeBook.FileName);
            GC.Collect();
            MessageBox.Show("done");
        }

        private void button6_Click(object sender, EventArgs e)
        {
            loadstreamfile.ShowDialog();
            Librarian_KUKA librarian = new Librarian_KUKA();
            routeBook = librarian.LoadFile(loadstreamfile.SelectedPath, "TestFile");
            GC.Collect();
            MessageBox.Show("done");
        }

        private void button7_Click(object sender, EventArgs e)
        {
            loadPOS.ShowDialog();
        }

        private void button8_Click(object sender, EventArgs e)
        {
            string path = ".\\axis.txt";
            StreamWriter _streamWriter = new StreamWriter(path, false);
            for (int i = 0; i < kuka.currentAXIS.count; i++)
            {
                float[] tmp = kuka.currentAXIS.queue.Dequeue();
                _streamWriter.Write(tmp[0].ToString() + ","
                                  + tmp[1].ToString() + ","
                                  + tmp[2].ToString() + ","
                                  + tmp[3].ToString() + ","
                                  + tmp[4].ToString() + ","
                                  + tmp[5].ToString() + Environment.NewLine);
            }
            _streamWriter.Close();
        }

        private void tmr_statusupdate_Tick(object sender, EventArgs e)
        {
            textBox6.Text = kuka.ConnectState;
            if (textBox6.Text == "stable")
            {
                button1.Text = "connect";
                kuka.kRC_SETOVERRIDE.OVERRIDE = Convert.ToInt16(trackBar1.Value);

                //jog (Justin)
                ReadJogState();
            }
            else
                button1.Text = "disconnect";
            textBox7.Text = kuka.ErrorId.ToString();
            textBox8.Text = kuka.ResetCount.ToString();
            textBox10.Text = kuka.FinishCount.ToString();
            textBox11.Text = kuka.CurrentState.ToString();
            textBox9.Text = kuka.kRC_SETOVERRIDE.OVERRIDE.ToString();

            textBox3.Text = kuka.kRC_READACTUALPOSITION.X.ToString();
            textBox4.Text = kuka.kRC_READACTUALPOSITION.Y.ToString();
            textBox5.Text = kuka.kRC_READACTUALPOSITION.Z.ToString();
            textBox13.Text = kuka.kRC_READACTUALPOSITION.A.ToString();
            textBox14.Text = kuka.kRC_READACTUALPOSITION.B.ToString();
            textBox15.Text = kuka.kRC_READACTUALPOSITION.C.ToString();


            

        }

        /// <summary>
        /// read jog state (justin)
        /// </summary>
        private void ReadJogState()
        {

            kuka.Robot_JogReadState(ref _busy, ref _active, ref _done, ref _aborted, ref _error, ref _errorID);
            label3.Text = "Busy : "+ _busy.ToString();
            label4.Text = "Active : " + _active.ToString();
            label5.Text = "Done : " + _done.ToString();
            label13.Text = "Aborted : " + _aborted.ToString();
            label14.Text = "Error : " + _error.ToString();
            label15.Text = "ErrorID : " + _errorID.ToString();
        }


        Queue<float[]> queue;
        RouteBook_KUKA routeBook;
        private void loadroutefile_FileOk(object sender, CancelEventArgs e)
        {
            queue = null;
            queue = new Queue<float[]>();

            string _filepath = loadroutefile.FileName;
            StreamReader _streamReader = new StreamReader(_filepath);
            while (!_streamReader.EndOfStream)
            {
                string _stringloader = _streamReader.ReadLine();
                string[] _stringarray = _stringloader.Split(',');
                float[] _floatarray = new float[_stringarray.Length];
                for (int i = 0; i < _stringarray.Length; i++) _floatarray[i] = Convert.ToSingle(_stringarray[i]);
                queue.Enqueue(_floatarray);
            }

            float[] _position;
            routeBook = new RouteBook_KUKA("TestFile", queue.Count, 0);
            for (int i = 0; i < routeBook.PointNumber + routeBook.DoutNumber; i++)
            {
                //if (i == 10)
                //    routeBook.ProcessQueue[i] = 2;
                //else if (i == 15)
                //    routeBook.ProcessQueue[i] = 2;
                //else
                routeBook.ProcessQueue[i] = 1;
            }
            for (int i = 0; i < routeBook.PointNumber; i++)
            {
                routeBook.MovingMode[i] = Convert.ToInt32(textBox2.Text);
                routeBook.Tool[i] = 1f;
                routeBook.Workspace[i] = 1f;

                routeBook.Override[i] = Convert.ToInt32(textBox9.Text);
                routeBook.Accerlerate[i] = 70;
                routeBook.ContinueMove[i] = true;

                _position = queue.Dequeue();
                if (routeBook.MovingMode[i] == 4)
                {
                    routeBook.X[i] = _position[0];
                    routeBook.Y[i] = _position[1];
                    routeBook.Z[i] = _position[2];
                    routeBook.A[i] = _position[3];
                    routeBook.B[i] = _position[4];
                    routeBook.C[i] = _position[5];
                }
                else
                {
                    routeBook.X[i] = _position[0];
                    routeBook.Y[i] = _position[1];
                    routeBook.Z[i] = _position[2];// + 100;
                    routeBook.A[i] = _position[3];
                    routeBook.B[i] = _position[4];
                    routeBook.C[i] = _position[5];
                    //routeBook.S[i] = (short)6;
                    //routeBook.T[i] = (short)18;
                }
            }
            //routeBook.DOutMode[0] = 13;
            //routeBook.DOutMode[1] = -13;
        }


        private void TransferAndUpdateToRobot(string FilePath)
        {
            queue = null;
            queue = new Queue<float[]>();

            string _filepath = FilePath;
            StreamReader _streamReader = new StreamReader(_filepath);
            while (!_streamReader.EndOfStream)
            {
                string _stringloader = _streamReader.ReadLine();
                string[] _stringarray = _stringloader.Split(',');
                float[] _floatarray = new float[_stringarray.Length];
                for (int i = 0; i < _stringarray.Length; i++) _floatarray[i] = Convert.ToSingle(_stringarray[i]);
                queue.Enqueue(_floatarray);
            }

            float[] _position;
            routeBook = new RouteBook_KUKA("TestFile", queue.Count, 0);
            for (int i = 0; i < routeBook.PointNumber + routeBook.DoutNumber; i++)
            {
                //if (i == 10)
                //    routeBook.ProcessQueue[i] = 2;
                //else if (i == 15)
                //    routeBook.ProcessQueue[i] = 2;
                //else
                routeBook.ProcessQueue[i] = 1;
            }
            for (int i = 0; i < routeBook.PointNumber; i++)
            {
                routeBook.MovingMode[i] = Convert.ToInt32(textBox2.Text);
                routeBook.Tool[i] = 1f;
                routeBook.Workspace[i] = 1f;

                routeBook.Override[i] = Convert.ToInt32(textBox9.Text);
                routeBook.Accerlerate[i] = 70;
                routeBook.ContinueMove[i] = true;

                _position = queue.Dequeue();
                if (routeBook.MovingMode[i] == 4)
                {
                    routeBook.X[i] = _position[0];
                    routeBook.Y[i] = _position[1];
                    routeBook.Z[i] = _position[2];
                    routeBook.A[i] = _position[3];
                    routeBook.B[i] = _position[4];
                    routeBook.C[i] = _position[5];
                }
                else
                {
                    routeBook.X[i] = _position[0];
                    routeBook.Y[i] = _position[1];
                    routeBook.Z[i] = _position[2];// + 100;
                    routeBook.A[i] = _position[3];
                    routeBook.B[i] = _position[4];
                    routeBook.C[i] = _position[5];
                    //routeBook.S[i] = (short)6;
                    //routeBook.T[i] = (short)18;
                }
            }
            //routeBook.DOutMode[0] = 13;
            //routeBook.DOutMode[1] = -13;
        }

        RefAXIS refAXIS;
        private void loadPOS_FileOk(object sender, CancelEventArgs e)
        {
            queue = null;
            queue = new Queue<float[]>();

            string _filepath = loadPOS.FileName;
            StreamReader _streamReader = new StreamReader(_filepath);
            while (!_streamReader.EndOfStream)
            {
                string _stringloader = _streamReader.ReadLine();
                string[] _stringarray = _stringloader.Split(',');
                float[] _floatarray = new float[_stringarray.Length];
                for (int i = 0; i < _stringarray.Length; i++) _floatarray[i] = Convert.ToSingle(_stringarray[i]);
                queue.Enqueue(_floatarray);
            }

            float[] _position;
            routeBook = null;
            routeBook = new RouteBook_KUKA("POSFile", queue.Count, 0);
            for (int i = 0; i < routeBook.PointNumber + routeBook.DoutNumber; i++)
            {
                _position = queue.Dequeue();
                routeBook.X[i] = _position[0];
                routeBook.Y[i] = _position[1];
                routeBook.Z[i] = _position[2];
                routeBook.A[i] = _position[3];
                routeBook.B[i] = _position[4];
                routeBook.C[i] = _position[5];
            }

            refAXIS = kuka.SaveAXIS(routeBook, 0, -90, 90, 0, -90, -180);
            kuka.RunInverse(refAXIS);
        }

        private void button9_Click(object sender, EventArgs e)
        {
            queue = null;
            queue = new Queue<float[]>();

            string _filepath = ".\\origin.txt";
            StreamReader _streamReader = new StreamReader(_filepath);
            while (!_streamReader.EndOfStream)
            {
                string _stringloader = _streamReader.ReadLine();
                string[] _stringarray = _stringloader.Split(',');
                float[] _floatarray = new float[_stringarray.Length];
                for (int i = 0; i < _stringarray.Length; i++) _floatarray[i] = Convert.ToSingle(_stringarray[i]);
                queue.Enqueue(_floatarray);
            }

            string path = ".\\relative.txt";
            StreamWriter _streamWriter = new StreamWriter(path, false);

            float x, y, z, a, b, c;
            float[] tmp = queue.Dequeue();
            x = tmp[0]; y = tmp[1]; z = tmp[2]; a = tmp[3]; b = tmp[4]; c = tmp[5];
            _streamWriter.Write("lin {" +
                                "x " + x.ToString() + "," +
                                "y " + y.ToString() + "," +
                                "z " + z.ToString() + "," +
                                "a " + a.ToString() + "," +
                                "b " + b.ToString() + "," +
                                "c " + c.ToString() + "}" +
                                " c_dis" + Environment.NewLine);
            int len = queue.Count;
            for (int i = 0; i < len; i++)
            {
                tmp = queue.Dequeue();
                _streamWriter.Write("lin_rel {" +
                                    "x " + (tmp[0] - x).ToString() + "," +
                                    "y " + (tmp[1] - y).ToString() + "," +
                                    "z " + (tmp[2] - z).ToString() + "," +
                                    "a " + (tmp[3] - a).ToString() + "," +
                                    "b " + (tmp[4] - b).ToString() + "," +
                                    "c " + (tmp[5] - c).ToString() + "}" +
                                    " c_dis" + Environment.NewLine);
                x = tmp[0]; y = tmp[1]; z = tmp[2]; a = tmp[3]; b = tmp[4]; c = tmp[5];
            }
            _streamWriter.Close();
        }

        public async Task button11_Click(object sender, EventArgs e)
        {
            string[] files = new string[] { "P-right-0 (對位孔上方).txt", "P-right-1 (插進對位孔).txt" };


            for (int i = 0; i < files.Length; i++)
            {
                TransferAndUpdateToRobot("D://Hand_route//" + files[i]);
                var route = kuka.SaveFile(routeBook);

                await Task.Delay(100);
                kuka.RunProgram(route);
                await Task.Delay(100);

            }
            while (kuka.CurrentState != 1)
            {
                await Task.Delay(50);
            }
        }

        public async Task button12_Click(object sender, EventArgs e)
        {
            string[] files = new string[] { "P-right-2 (從料盤架上拉出).txt", "HomePointion.txt", "L-FootPath.txt"};


            for (int i = 0; i < files.Length; i++)
            {
                TransferAndUpdateToRobot("D://Hand_route//" + files[i]);
                var route = kuka.SaveFile(routeBook);

                if (files[i] == "L-FootPath.txt")
                {
                    await Task.Delay(1000);
                }
                else
                {
                    await Task.Delay(100);
                }
                
                kuka.RunProgram(route);

                if (files[i] == "L-FootPath.txt")
                {
                    await Task.Delay(1000);
                }
                else
                {
                    await Task.Delay(100);
                }

            }
            while (kuka.CurrentState != 1)
            {
                await Task.Delay(100);
            }
        }

        public async Task PutShoeBack()
        {
            string[] files = new string[] { "HomePointion.txt", "P-right-3 (插入料盤架前).txt", "P-right-4 (插入料盤架P1).txt", "P-right-5 (插入料盤架P2).txt" };


            for (int i = 0; i < files.Length; i++)
            {
                TransferAndUpdateToRobot("D://Hand_route//" + files[i]);
                var route = kuka.SaveFile(routeBook);
                
                await Task.Delay(100);
                kuka.RunProgram(route);
                await Task.Delay(100);
                
            }
            while (kuka.CurrentState != 1)
            {
                await Task.Delay(100);
            }
        }

        public async Task button13_Click(object sender, EventArgs e)
        {
            string[] files = new string[] { "P-right-0 (對位孔上方).txt", "HomePointion.txt" };


            for (int i = 0; i < files.Length; i++)
            {
                TransferAndUpdateToRobot("D://Hand_route//" + files[i]);
                kukaRoute = kuka.SaveFile(routeBook);

                await Task.Delay(50);
                kuka.RunProgram(kukaRoute);
                //do
                //{

                //}
                //while (kuka.FinishCount != kukaRoute.routeBook.PointNumber);

                await Task.Delay(100);

            }
            while (kuka.CurrentState != 1)
            {
                await Task.Delay(50);
            }
            //MessageBox.Show("finish!");
        }


        #region Justin Jog test


        /// <summary>
        /// Button(MouseDown) 集合 ---> Justin
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonControl_MouseDown(object sender, MouseEventArgs e)
        {
            #region for **** button

            #endregion

            Button btn = (Button)sender; //轉型            
            string m_btnName = (string)btn.Tag;
            switch (m_btnName)
            {

                #region JOG (A1_X_+)
                case "A1_X_+":

                    try
                    {
                        kuka.Robot_JogStart("A1_X_P", kuka.jogData, kuka.cORS_COR);
                    }
                    catch (Exception x) { MessageBox.Show(x.ToString(), "systen error!!!"); }

                    break;
                #endregion

                #region JOG (A1_X_-)
                case "A1_X_-":

                    try
                    {
                        kuka.Robot_JogStart("A1_X_M", kuka.jogData, kuka.cORS_COR);
                    }
                    catch (Exception x) { MessageBox.Show(x.ToString(), "systen error!!!"); }

                    break;
                #endregion

                #region JOG (A2_Y_+)
                case "A2_Y_+":

                    try
                    {
                        kuka.Robot_JogStart("A2_Y_P", kuka.jogData, kuka.cORS_COR);
                    }
                    catch (Exception x) { MessageBox.Show(x.ToString(), "systen error!!!"); }

                    break;
                #endregion

                #region JOG (A2_Y_-)
                case "A2_Y_-":

                    try
                    {
                        kuka.Robot_JogStart("A2_Y_M", kuka.jogData, kuka.cORS_COR);
                    }
                    catch (Exception x) { MessageBox.Show(x.ToString(), "systen error!!!"); }

                    break;
                #endregion


                #region JOG (A3_Z_+)
                case "A3_Z_+":

                    try
                    {
                        kuka.Robot_JogStart("A3_Z_P", kuka.jogData, kuka.cORS_COR);
                    }
                    catch (Exception x) { MessageBox.Show(x.ToString(), "systen error!!!"); }

                    break;
                #endregion

                #region JOG (A3_Z_-)
                case "A3_Z_-":

                    try
                    {
                        kuka.Robot_JogStart("A3_Z_M", kuka.jogData, kuka.cORS_COR);
                    }
                    catch (Exception x) { MessageBox.Show(x.ToString(), "systen error!!!"); }

                    break;
                #endregion

                #region JOG (A4_RX_+)
                case "A4_RX_+":

                    try
                    {
                        kuka.Robot_JogStart("A4_A_P", kuka.jogData, kuka.cORS_COR);
                    }
                    catch (Exception x) { MessageBox.Show(x.ToString(), "systen error!!!"); }

                    break;
                #endregion

                #region JOG (A4_RX_-)
                case "A4_RX_-":

                    try
                    {
                        kuka.Robot_JogStart("A4_A_M", kuka.jogData, kuka.cORS_COR);
                    }
                    catch (Exception x) { MessageBox.Show(x.ToString(), "systen error!!!"); }

                    break;
                #endregion

                #region JOG (A5_RY_+)
                case "A5_RY_+":

                    try
                    {
                        kuka.Robot_JogStart("A5_B_P", kuka.jogData, kuka.cORS_COR);
                    }
                    catch (Exception x) { MessageBox.Show(x.ToString(), "systen error!!!"); }

                    break;
                #endregion

                #region JOG (A5_RY_-)
                case "A5_RY_-":

                    try
                    {
                        kuka.Robot_JogStart("A5_B_M", kuka.jogData, kuka.cORS_COR);
                    }
                    catch (Exception x) { MessageBox.Show(x.ToString(), "systen error!!!"); }

                    break;
                #endregion


                #region JOG (A6_RZ_+)
                case "A6_RZ_+":

                    try
                    {
                        kuka.Robot_JogStart("A6_C_P", kuka.jogData, kuka.cORS_COR);
                    }
                    catch (Exception x) { MessageBox.Show(x.ToString(), "systen error!!!"); }

                    break;
                #endregion

                #region JOG (A3_Z_-)
                case "A6_RZ_-":

                    try
                    {
                        kuka.Robot_JogStart("A6_C_M", kuka.jogData, kuka.cORS_COR);
                    }
                    catch (Exception x) { MessageBox.Show(x.ToString(), "systen error!!!"); }

                    break;
                #endregion

                #region 測試按鈕
                case "test_btn":

                    try
                    {

                    }
                    catch (Exception x) { MessageBox.Show(x.ToString(), "systen error!!!"); }


                    break;
                    #endregion

            }
        }

        /// <summary>
        /// Button(MouseUp) 集合 ---> Justin
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonControl_MouseUp(object sender, MouseEventArgs e)
        {
            #region for **** button

            #endregion

            Button btn = (Button)sender; //轉型            
            string m_btnName = (string)btn.Tag;
            switch (m_btnName)
            {

                #region JOG (A1_X_+)
                case "A1_X_+":

                    try
                    {
                        kuka.Robot_JogStop("A1_X_P", kuka.jogData, kuka.cORS_COR);
                    }
                    catch (Exception x) { MessageBox.Show(x.ToString(), "systen error!!!"); }

                    break;
                #endregion

                #region JOG (A1_X_-)
                case "A1_X_-":

                    try
                    {
                        kuka.Robot_JogStop("A1_X_M", kuka.jogData, kuka.cORS_COR);
                    }
                    catch (Exception x) { MessageBox.Show(x.ToString(), "systen error!!!"); }

                    break;
                #endregion

                #region JOG (A2_Y_+)
                case "A2_Y_+":

                    try
                    {
                        kuka.Robot_JogStop("A2_Y_P", kuka.jogData, kuka.cORS_COR);
                    }
                    catch (Exception x) { MessageBox.Show(x.ToString(), "systen error!!!"); }

                    break;
                #endregion

                #region JOG (A2_Y_-)
                case "A2_Y_-":

                    try
                    {
                        kuka.Robot_JogStop("A2_Y_M", kuka.jogData, kuka.cORS_COR);
                    }
                    catch (Exception x) { MessageBox.Show(x.ToString(), "systen error!!!"); }

                    break;
                #endregion


                #region JOG (A3_Z_+)
                case "A3_Z_+":

                    try
                    {
                        kuka.Robot_JogStop("A3_Z_P", kuka.jogData, kuka.cORS_COR);
                    }
                    catch (Exception x) { MessageBox.Show(x.ToString(), "systen error!!!"); }

                    break;
                #endregion

                #region JOG (A3_Z_-)
                case "A3_Z_-":

                    try
                    {
                        kuka.Robot_JogStop("A3_Z_M", kuka.jogData, kuka.cORS_COR);
                    }
                    catch (Exception x) { MessageBox.Show(x.ToString(), "systen error!!!"); }

                    break;
                #endregion

                #region JOG (A4_RX_+)
                case "A4_RX_+":

                    try
                    {
                        kuka.Robot_JogStop("A4_A_P", kuka.jogData, kuka.cORS_COR);
                    }
                    catch (Exception x) { MessageBox.Show(x.ToString(), "systen error!!!"); }

                    break;
                #endregion

                #region JOG (A4_RX_-)
                case "A4_RX_-":

                    try
                    {
                        kuka.Robot_JogStop("A4_A_M", kuka.jogData, kuka.cORS_COR);
                    }
                    catch (Exception x) { MessageBox.Show(x.ToString(), "systen error!!!"); }

                    break;
                #endregion

                #region JOG (A5_RY_+)
                case "A5_RY_+":

                    try
                    {
                        kuka.Robot_JogStop("A5_B_P", kuka.jogData, kuka.cORS_COR);
                    }
                    catch (Exception x) { MessageBox.Show(x.ToString(), "systen error!!!"); }

                    break;
                #endregion

                #region JOG (A5_RY_-)
                case "A5_RY_-":

                    try
                    {
                        kuka.Robot_JogStop("A5_B_M", kuka.jogData, kuka.cORS_COR);
                    }
                    catch (Exception x) { MessageBox.Show(x.ToString(), "systen error!!!"); }

                    break;
                #endregion


                #region JOG (A6_RZ_+)
                case "A6_RZ_+":

                    try
                    {
                        kuka.Robot_JogStop("A6_C_P", kuka.jogData, kuka.cORS_COR);
                    }
                    catch (Exception x) { MessageBox.Show(x.ToString(), "systen error!!!"); }

                    break;
                #endregion

                #region JOG (A3_Z_-)
                case "A6_RZ_-":

                    try
                    {
                        kuka.Robot_JogStop("A6_C_M", kuka.jogData, kuka.cORS_COR);
                    }
                    catch (Exception x) { MessageBox.Show(x.ToString(), "systen error!!!"); }

                    break;
                #endregion

                #region 測試按鈕
                case "test_btn":

                    try
                    {

                    }
                    catch (Exception x) { MessageBox.Show(x.ToString(), "systen error!!!"); }


                    break;
                    #endregion

            }
        }


        #endregion
    }
}