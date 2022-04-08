using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;

using MxA;

namespace RouteButler_KUKA
{
    public class KukaRoute
    {
        public int ReferencePosture;
        public bool[] ActiveMode;
        public RouteBook_KUKA routeBook;
        public E6POS[] e6POS;//x,y,z,a,b,c,s,t
        public E6AXIS[] e6AXIS;//a1,a2,a3,a4,a5,a6
        public COORDSYS[] cOORDSYS;//tool 1~16 ; base 1~32
        public KRC_MOVELINEARABSOLUTE[] kRC_MOVELINEARABSOLUTE;
        public KRC_MOVESLINEARABSOLUTE[] kRC_MOVESLINEARABSOLUTE;
        public KRC_MOVEDIRECTABSOLUTE[] kRC_MOVEDIRECTABSOLUTE;
        public KRC_MOVEAXISABSOLUTE[] kRC_MOVEAXISABSOLUTE;
        public KRC_SETDISTANCETRIGGER[] kRC_SETDISTANCETRIGGER;//1~1024

    }

    public class RefAXIS
    {
        public int count, counter;
        public bool[] ActiveMode;
        public E6AXIS refAXIS = new E6AXIS();
        public E6POS[] e6POS;
        public Queue<float[]> queue;
        public KRC_INVERSE[] kRC_INVERSE;
    }

    public class RefPOS
    {
        public int count, counter;
        public bool[] ActiveMode;
        public Queue<float[]> queue;
        public E6AXIS[] e6AXIS;
        public KRC_FORWARD[] kRC_FORWARD;
    }

    public class Kuka
    {
        public Kuka()
        {
            MerryGoRound.WorkerSupportsCancellation = true;
            MerryGoRound.WorkerReportsProgress = true;
            MerryGoRound.DoWork += MerryGoRound_DoWork;
            MerryGoRound.RunWorkerCompleted += MerryGoRound_RunWorkerCompleted;

            GLOBAL.KRC_AXISGROUPREFARR[GroupId].HEARTBEATTO = 2000;
            GLOBAL.KRC_AXISGROUPREFARR[GroupId].DEF_VEL_CP = 2;
            GLOBAL.KRC_AXISGROUPREFARR[GroupId].KRCSTATE.POSACTVALID = true;
        }

        public string ConnectState;
        public int ReceiveTimeout = 0, ErrorId = 0, ResetCount = 0, FinishCount = 0, CurrentState = 0;

        private readonly short GroupId = 1;
        private byte[] KRCInput = new byte[256], KRCOutput = new byte[256];
        
        private KRC_READAXISGROUP kRC_READAXISGROUP = new KRC_READAXISGROUP();
        private KRC_WRITEAXISGROUP kRC_WRITEAXISGROUP = new KRC_WRITEAXISGROUP();
        private KRC_DIAG kRC_DIAG = new KRC_DIAG();
        private KRC_ERROR kRC_ERROR = new KRC_ERROR();
        private KRC_INITIALIZE kRC_INITIALIZE = new KRC_INITIALIZE();
        private KRC_AUTOSTART kRC_AUTOSTART = new KRC_AUTOSTART();
        private KRC_AUTOMATICEXTERNAL kRC_AUTOMATICEXTERNAL = new KRC_AUTOMATICEXTERNAL();
        public KRC_SETOVERRIDE kRC_SETOVERRIDE = new KRC_SETOVERRIDE();
        //        private KRC_SETOVERRIDE kRC_SETOVERRIDE = new KRC_SETOVERRIDE();
        public KRC_READACTUALPOSITION kRC_READACTUALPOSITION = new KRC_READACTUALPOSITION();
        private KRC_WRITESYSVAR kRC_WRITESYSVAR = new KRC_WRITESYSVAR();
        private KRC_SETADVANCE kRC_SETADVANCE = new KRC_SETADVANCE();

        private ConcurrentQueue<bool> FlagPower = new ConcurrentQueue<bool>(), FlagReset = new ConcurrentQueue<bool>(), FlagResetAlarm = new ConcurrentQueue<bool>();
        private ConcurrentQueue<int> CycleType = new ConcurrentQueue<int>();
        private UdpClient SocketSend, SocketReceive;
        private IPEndPoint SendEndPoint, ReceiveEndPoint;
        private Stopwatch ElapseWatch = new Stopwatch();

        private KukaRoute currentRoute;
        public RefAXIS currentAXIS;
        public RefPOS currentPOS;




        #region Justin Jog test

        /// <summary>
        /// 實作Jog 物件
        /// </summary>
        private KRC_JOG kRC_jog = new KRC_JOG();

        public class JogData
        {
            /// <summary>
            /// 軸組索引
            /// </summary>
            public short groupId;
            /// <summary>
            /// (0: Axis-specific (1: Cartesian (2: Axis-specific, as spline motion (3: Cartesian, as spline motion
            /// </summary>
            public short moveType;
            /// <summary>
            /// 速度 0~100%
            /// </summary>
            public short velocity;
            /// <summary>
            /// 加速度 0~100%
            /// </summary>
            public short acceleration;
            /// <summary>
            /// 增量
            /// </summary>
            public float increment;
        }



        /// <summary>
        /// 坐標系定義
        /// </summary>
        public COORDSYS cORS_COR = new COORDSYS
        {
            BASE = -1, //基座標
            IPO_MODE = 0, //IPO_MODE
            TOOL = -1 //工具座標
        };
        /// <summary>
        /// Jog 位移資料
        /// </summary>
        public JogData jogData = new JogData
        {
            groupId = 1, 
            moveType = 1, 
            velocity = 10, 
            acceleration = 10, 
            increment = 0
        };

        /// <summary>
        /// 觸發方法 Jog 啟動
        /// </summary>
        /// <param name="_jogAxis"></param>
        /// <param name="_jogData"></param>
        /// <param name="_cORS_COR"></param>
        public void Robot_JogStart(string _jogAxis,JogData _jogData, COORDSYS _cORS_COR)
        {
            kRC_jog.AXISGROUPIDX = _jogData.groupId;
            kRC_jog.MOVETYPE = _jogData.moveType;
            kRC_jog.VELOCITY = _jogData.velocity;
            kRC_jog.ACCELERATION = _jogData.acceleration;
            kRC_jog.COORDINATESYSTEM = _cORS_COR;
            kRC_jog.INCREMENT = _jogData.increment;
            //觸發軸向 (P=正向  (M=反向
            switch (_jogAxis)
            {
                #region A1_X_M
                case "A1_X_M":
                    kRC_jog.A1_X_M = true;
                    /*
                    kRC_jog.A1_X_P = false;
                    kRC_jog.A2_Y_M = false;
                    kRC_jog.A2_Y_P = false;
                    kRC_jog.A3_Z_M = false;
                    kRC_jog.A3_Z_P = false;
                    kRC_jog.A4_A_M = false;
                    kRC_jog.A4_A_P = false;
                    kRC_jog.A5_B_M = false;
                    kRC_jog.A5_B_P = false;
                    kRC_jog.A6_C_M = false;
                    kRC_jog.A6_C_P = false;
                    */


                    kRC_jog.OnCycle();
                    break;
                #endregion

                #region A1_X_P
                case "A1_X_P":
                    kRC_jog.A1_X_P = true;
                    break;
                #endregion

                #region A2_Y_M
                case "A2_Y_M":
                    kRC_jog.A2_Y_M = true;
                    break;
                #endregion

                #region A2_Y_P
                case "A2_Y_P":
                    kRC_jog.A2_Y_P = true;
                    break;
                #endregion

                #region A3_Z_M
                case "A3_Z_M":
                    kRC_jog.A3_Z_M = true;
                    break;
                #endregion

                #region A3_Z_P
                case "A3_Z_P":
                    kRC_jog.A3_Z_P = true;
                    break;
                #endregion

                #region A4_A_M
                case "A4_A_M":
                    kRC_jog.A4_A_M=true;
                    break;
                #endregion

                #region A4_A_P
                case "A4_A_P":
                    kRC_jog.A4_A_P = true;
                    break;
                #endregion

                #region A5_B_M
                case "A5_B_M":
                    kRC_jog.A5_B_M = true;
                    break;
                #endregion

                #region A5_B_P
                case "A5_B_P":
                    kRC_jog.A5_B_P = true;
                    break;
                #endregion

                #region A6_C_M
                case "A6_C_M":
                    kRC_jog.A6_C_M = true;
                    break;
                #endregion

                #region A6_C_P
                case "A6_C_P":
                    kRC_jog.A6_C_P = true;
                    break;
                    #endregion



            }




        }

        /// <summary>
        /// 觸發方法 Jog 關閉
        /// </summary>
        /// <param name="_jogAxis"></param>
        /// <param name="_jogData"></param>
        /// <param name="_cORS_COR"></param>
        public void Robot_JogStop(string _jogAxis, JogData _jogData, COORDSYS _cORS_COR)
        {
            kRC_jog.AXISGROUPIDX = _jogData.groupId;
            kRC_jog.MOVETYPE = _jogData.moveType;
            kRC_jog.VELOCITY = _jogData.velocity;
            kRC_jog.ACCELERATION = _jogData.acceleration;
            kRC_jog.COORDINATESYSTEM = _cORS_COR;
            kRC_jog.INCREMENT = _jogData.increment;
            //觸發軸向 (P=正向  (M=反向
            switch (_jogAxis)
            {
                #region A1_X_M
                case "A1_X_M":
                    kRC_jog.A1_X_M = false;
                    /*
                    kRC_jog.A1_X_P = false;
                    kRC_jog.A2_Y_M = false;
                    kRC_jog.A2_Y_P = false;
                    kRC_jog.A3_Z_M = false;
                    kRC_jog.A3_Z_P = false;
                    kRC_jog.A4_A_M = false;
                    kRC_jog.A4_A_P = false;
                    kRC_jog.A5_B_M = false;
                    kRC_jog.A5_B_P = false;
                    kRC_jog.A6_C_M = false;
                    kRC_jog.A6_C_P = false;
                    */
                    kRC_jog.OnCycle();
                    break;
                #endregion

                #region A1_X_P
                case "A1_X_P":
                    kRC_jog.A1_X_P = false;
                    break;
                #endregion

                #region A2_Y_M
                case "A2_Y_M":
                    kRC_jog.A2_Y_M = false;
                    break;
                #endregion

                #region A2_Y_P
                case "A2_Y_P":
                    kRC_jog.A2_Y_P = false;
                    break;
                #endregion

                #region A3_Z_M
                case "A3_Z_M":
                    kRC_jog.A3_Z_M = false;
                    break;
                #endregion

                #region A3_Z_P
                case "A3_Z_P":
                    kRC_jog.A3_Z_P = false;
                    break;
                #endregion

                #region A4_A_M
                case "A4_A_M":
                    kRC_jog.A4_A_M = false;
                    break;
                #endregion

                #region A4_A_P
                case "A4_A_P":
                    kRC_jog.A4_A_P = false;
                    break;
                #endregion

                #region A5_B_M
                case "A5_B_M":
                    kRC_jog.A5_B_M = false;
                    break;
                #endregion

                #region A5_B_P
                case "A5_B_P":
                    kRC_jog.A5_B_P = false;
                    break;
                #endregion

                #region A6_C_M
                case "A6_C_M":
                    kRC_jog.A6_C_M = false;
                    break;
                #endregion

                #region A6_C_P
                case "A6_C_P":
                    kRC_jog.A6_C_P = false;
                    break;
                    #endregion

            }




        }


        /// <summary>
        /// Jog 狀態
        /// </summary>
        /// <param name="busy"></param>
        /// <param name="active"></param>
        /// <param name="done"></param>
        /// <param name="aborted"></param>
        /// <param name="error"></param>
        /// <param name="errorID"></param>
        public void Robot_JogReadState(ref bool busy,ref bool active,ref bool done,ref bool aborted,ref bool error,ref int errorID)
        {
            busy = kRC_jog.BUSY;
            active = kRC_jog.ACTIVE;
            done = kRC_jog.DONE;
            aborted = kRC_jog.ABORTED;
            error = kRC_jog.ERROR;
            errorID = kRC_jog.ERRORID;
        }



        #endregion


        private APO pTP_APO = new APO
        {
            PTP_MODE = (short)1,
            CP_MODE = (short)0,
            CPTP = 50,
            CDIS = 1f,
            CORI = 1f,
            CVEL = 50
        };
        private APO pTP_CP_APO = new APO
        {
            PTP_MODE = (short)4,
            CP_MODE = (short)0,
            CPTP = 50,
            CDIS = 1f,
            CORI = 1f,
            CVEL = 50
        };
        private APO cP_APO = new APO
        {
            PTP_MODE = (short)0,
            CP_MODE = (short)3,
            CDIS = 1f,
            CORI = 1f,
            CVEL = 70
        };
        private APO stop_APO = new APO
        {
            PTP_MODE = (short)0,
            CP_MODE = (short)0
        };




        private BackgroundWorker MerryGoRound = new BackgroundWorker();

        private void MerryGoRound_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            ElapseWatch.Stop();
        }

        private void MerryGoRound_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                bool _flagPower = true, _flagReset = true, _tmpBool;
                double _cycleTime = 30.00d;
                int _tmpInt = 1;

                CurrentState = 0;
                ReceiveTimeout = 0;
                ResetCount = 0;
                ConnectState = "unstable";
                ElapseWatch.Reset();

                while (_flagPower)
                {
                    ElapseWatch.Restart();

                    if (FlagPower.TryPeek(out _tmpBool))
                        FlagPower.TryDequeue(out _flagPower);
                    if (FlagReset.TryPeek(out _tmpBool))
                        FlagReset.TryDequeue(out _flagReset);
                    if (CycleType.TryPeek(out _tmpInt))
                        CycleType.TryDequeue(out CurrentState);

                    ReadAxisGroup();

                    switch (CurrentState)
                    {
                        case 1:
                            InitialKRC(_flagReset);

                            FinishCount = 0;
                            currentRoute = null;

                            _cycleTime = 15.00d;
                            break;
                        case 2:
                            InitialKRC(_flagReset);
                            if (!_flagReset)
                            {
                                _tmpInt = Upload2controller();
                                if (_tmpInt == 0)
                                    CycleType.Enqueue(3);
                            }
                            _cycleTime = 15.00d;
                            break;
                        case 3:
                            InitialKRC(_flagReset);
                            if (!_flagReset)
                            {
                                _tmpInt = RunProgram();
                                if (_tmpInt == 0)
                                {
                                    currentRoute = null;
                                    kRC_WRITESYSVAR.EXECUTECMD = false;
                                    kRC_WRITESYSVAR.OnCycle();
                                    kRC_SETADVANCE.EXECUTECMD = false;
                                    kRC_SETADVANCE.OnCycle();
                                    CycleType.Enqueue(1);
                                }
                            }
                            else
                                CycleType.Enqueue(4);
                            _cycleTime = 5.00d;
                            break;
                        case 4:
                            InitialKRC(_flagReset);
                            DisposeProgram();
                            FinishCount = 0;
                            CycleType.Enqueue(1);
                            _cycleTime = 15.00d;
                            break;
                        case 5:
                            InitialKRC(_flagReset);
                            if (!_flagReset)
                            {
                                _tmpInt = UploadAXIS();
                                if (_tmpInt == 0)
                                    CycleType.Enqueue(6);
                            }
                            _cycleTime = 15.00d;
                            break;
                        case 6:
                            InitialKRC(_flagReset);
                            if (!_flagReset)
                            {
                                _tmpInt = RunInverse();
                                if (_tmpInt == 0)
                                {
                                    if (currentAXIS.counter > FinishCount)
                                    {
                                        FinishCount++;
                                        if (FinishCount == currentAXIS.count)
                                            CycleType.Enqueue(1);
                                        else
                                            CycleType.Enqueue(5);
                                    }
                                }
                            }
                            _cycleTime = 5.00d;
                            break;
                        case 7:
                            InitialKRC(_flagReset);
                            if (!_flagReset)
                            {
                                _tmpInt = UploadPOS();
                                if (_tmpInt == 0)
                                    CycleType.Enqueue(8);
                            }
                            _cycleTime = 15.00d;
                            break;
                        case 8:
                            InitialKRC(_flagReset);
                            if (!_flagReset)
                            {
                                _tmpInt = RunForward();
                                if (_tmpInt == 0)
                                    CycleType.Enqueue(1);
                            }
                            _cycleTime = 5.00d;
                            break;
                        default:
                            break;
                    }

                    WriteAxisGroup();

                    ConnectState = CheckKRC(_flagReset, ref ResetCount);

                    SpinWait.SpinUntil(() => ElapseWatch.Elapsed.TotalMilliseconds >= _cycleTime);
                }
            }
            catch (Exception ex)
            {
                MerryGoRound.CancelAsync();
            }
        }

        public RefAXIS SaveAXIS(RouteBook_KUKA _routeBook_KUKA, float _a1, float _a2, float _a3, float _a4, float _a5, float _a6)
        {
            RefAXIS refAXIS = new RefAXIS();

            refAXIS.refAXIS = new E6AXIS
            {
                A1 = _a1,
                A2 = _a2,
                A3 = _a3,
                A4 = _a4,
                A5 = _a5,
                A6 = _a6
            };

            refAXIS.count = _routeBook_KUKA.PointNumber;

            refAXIS.ActiveMode = new bool[refAXIS.count];
            refAXIS.e6POS = new E6POS[refAXIS.count];
            refAXIS.queue = new Queue<float[]>();
            refAXIS.kRC_INVERSE = new KRC_INVERSE[refAXIS.count];

            for (int i = 0; i < refAXIS.count; i++)
            {
                refAXIS.ActiveMode[i] = true;
                refAXIS.e6POS[i] = new E6POS
                {
                    X = _routeBook_KUKA.X[i],
                    Y = _routeBook_KUKA.Y[i],
                    Z = _routeBook_KUKA.Z[i],
                    A = _routeBook_KUKA.A[i],
                    B = _routeBook_KUKA.B[i],
                    C = _routeBook_KUKA.C[i]
                };
                refAXIS.kRC_INVERSE[i] = new KRC_INVERSE();
                refAXIS.kRC_INVERSE[i].AXISGROUPIDX = GroupId;
                refAXIS.kRC_INVERSE[i].EXECUTECMD = false;
                refAXIS.kRC_INVERSE[i].ACTPOSITION = refAXIS.e6POS[i];
                refAXIS.kRC_INVERSE[i].START_AXIS = refAXIS.refAXIS;
                refAXIS.kRC_INVERSE[i].POSVALIDS = false;
                refAXIS.kRC_INVERSE[i].POSVALIDT = false;
                refAXIS.kRC_INVERSE[i].CHECKSOFTEND = false;
                refAXIS.kRC_INVERSE[i].BUFFERMODE = 2;
            }

            return refAXIS;
        }

        public RefPOS SavePOS(RouteBook_KUKA _routeBook_KUKA)
        {
            RefPOS refPOS = new RefPOS();

            refPOS.count = _routeBook_KUKA.PointNumber;

            refPOS.ActiveMode = new bool[refPOS.count];
            refPOS.queue = new Queue<float[]>();
            refPOS.e6AXIS = new E6AXIS[refPOS.count];
            refPOS.kRC_FORWARD = new KRC_FORWARD[refPOS.count];

            for (int i = 0; i < refPOS.count; i++)
            {
                refPOS.ActiveMode[i] = true;
                refPOS.e6AXIS[i] = new E6AXIS
                {
                    A1 = _routeBook_KUKA.X[i],
                    A2 = _routeBook_KUKA.Y[i],
                    A3 = _routeBook_KUKA.Z[i],
                    A4 = _routeBook_KUKA.A[i],
                    A5 = _routeBook_KUKA.B[i],
                    A6 = _routeBook_KUKA.C[i]
                };
                refPOS.kRC_FORWARD[i] = new KRC_FORWARD();
                refPOS.kRC_FORWARD[i].AXISGROUPIDX = GroupId;
                refPOS.kRC_FORWARD[i].EXECUTECMD = false;
                refPOS.kRC_FORWARD[i].AXIS_VALUES = refPOS.e6AXIS[i];
                refPOS.kRC_FORWARD[i].CHECKSOFTEND = false;
                refPOS.kRC_FORWARD[i].BUFFERMODE = 2;
            }

            return refPOS;
        }

        public KukaRoute SaveFile(RouteBook_KUKA _routeBook)
        {
            KukaRoute kukaRoute = new KukaRoute
            {
                routeBook = _routeBook
            };

            kukaRoute.ActiveMode = new bool[kukaRoute.routeBook.PointNumber + kukaRoute.routeBook.DoutNumber];
            kukaRoute.kRC_MOVELINEARABSOLUTE = new KRC_MOVELINEARABSOLUTE[kukaRoute.routeBook.PointNumber];
            kukaRoute.kRC_MOVESLINEARABSOLUTE = new KRC_MOVESLINEARABSOLUTE[kukaRoute.routeBook.PointNumber];
            kukaRoute.kRC_MOVEDIRECTABSOLUTE = new KRC_MOVEDIRECTABSOLUTE[kukaRoute.routeBook.PointNumber];
            kukaRoute.kRC_MOVEAXISABSOLUTE = new KRC_MOVEAXISABSOLUTE[kukaRoute.routeBook.PointNumber];
            kukaRoute.kRC_SETDISTANCETRIGGER = new KRC_SETDISTANCETRIGGER[kukaRoute.routeBook.DoutNumber];
            kukaRoute.e6POS = new E6POS[kukaRoute.routeBook.PointNumber];
            kukaRoute.e6AXIS = new E6AXIS[kukaRoute.routeBook.PointNumber];
            kukaRoute.cOORDSYS = new COORDSYS[kukaRoute.routeBook.PointNumber];

            kRC_WRITESYSVAR.AXISGROUPIDX = GroupId;
            kRC_WRITESYSVAR.INDEX = 1;
            kRC_WRITESYSVAR.VALUE1 = 5;
            kRC_WRITESYSVAR.BCONTINUE = false;
            kRC_WRITESYSVAR.BUFFERMODE = 2;
            kRC_WRITESYSVAR.EXECUTECMD = false;

            kRC_SETADVANCE.AXISGROUPIDX = GroupId;
            kRC_SETADVANCE.COUNT = 5;
            kRC_SETADVANCE.BUFFERMODE = 2;
            kRC_SETADVANCE.EXECUTECMD = false;

            for (int i = 0; i < kukaRoute.routeBook.PointNumber + kukaRoute.routeBook.DoutNumber; i++)
                kukaRoute.ActiveMode[i] = true;

            for (int i = 0; i < kukaRoute.routeBook.PointNumber; i++)
            {
                kukaRoute.cOORDSYS[i] = new COORDSYS
                {
                    TOOL = Convert.ToInt16(kukaRoute.routeBook.Tool[i]),
                    BASE = Convert.ToInt16(kukaRoute.routeBook.Workspace[i])
                };
                switch (kukaRoute.routeBook.MovingMode[i])
                {
                    case 1:
                        kukaRoute.e6POS[i] = new E6POS
                        {
                            X = kukaRoute.routeBook.X[i],
                            Y = kukaRoute.routeBook.Y[i],
                            Z = kukaRoute.routeBook.Z[i],
                            A = kukaRoute.routeBook.A[i],
                            B = kukaRoute.routeBook.B[i],
                            C = kukaRoute.routeBook.C[i]
                        };
                        kukaRoute.kRC_MOVELINEARABSOLUTE[i] = new KRC_MOVELINEARABSOLUTE();
                        kukaRoute.kRC_MOVELINEARABSOLUTE[i].AXISGROUPIDX = GroupId;
                        kukaRoute.kRC_MOVELINEARABSOLUTE[i].ACTPOSITION = kukaRoute.e6POS[i];
                        kukaRoute.kRC_MOVELINEARABSOLUTE[i].COORDINATESYSTEM = kukaRoute.cOORDSYS[i];
                        if (kukaRoute.routeBook.ContinueMove[i] && i > 0 && i < kukaRoute.routeBook.PointNumber - 1)
                            kukaRoute.kRC_MOVELINEARABSOLUTE[i].APPROXIMATE = cP_APO;
                        else
                            kukaRoute.kRC_MOVELINEARABSOLUTE[i].APPROXIMATE = stop_APO;
                        kukaRoute.kRC_MOVELINEARABSOLUTE[i].VELOCITY = Convert.ToInt16(kukaRoute.routeBook.Override[i]);
                        kukaRoute.kRC_MOVELINEARABSOLUTE[i].ACCELERATION = Convert.ToInt16(kukaRoute.routeBook.Accerlerate[i]);
                        kukaRoute.kRC_MOVELINEARABSOLUTE[i].BUFFERMODE = 2;
                        kukaRoute.kRC_MOVELINEARABSOLUTE[i].EXECUTECMD = false;
                        break;
                    case 2:
                        kukaRoute.e6POS[i] = new E6POS
                        {
                            X = kukaRoute.routeBook.X[i],
                            Y = kukaRoute.routeBook.Y[i],
                            Z = kukaRoute.routeBook.Z[i],
                            A = kukaRoute.routeBook.A[i],
                            B = kukaRoute.routeBook.B[i],
                            C = kukaRoute.routeBook.C[i]
                        };
                        kukaRoute.kRC_MOVESLINEARABSOLUTE[i] = new KRC_MOVESLINEARABSOLUTE();
                        kukaRoute.kRC_MOVESLINEARABSOLUTE[i].AXISGROUPIDX = GroupId;
                        kukaRoute.kRC_MOVESLINEARABSOLUTE[i].ACTPOSITION = kukaRoute.e6POS[i];
                        kukaRoute.kRC_MOVESLINEARABSOLUTE[i].COORDINATESYSTEM = kukaRoute.cOORDSYS[i];
                        if (kukaRoute.routeBook.ContinueMove[i] && i > 0 && i < kukaRoute.routeBook.PointNumber - 1)
                            kukaRoute.kRC_MOVESLINEARABSOLUTE[i].APPROXIMATE = cP_APO;
                        else
                            kukaRoute.kRC_MOVESLINEARABSOLUTE[i].APPROXIMATE = stop_APO;
                        kukaRoute.kRC_MOVESLINEARABSOLUTE[i].VELOCITY = Convert.ToInt16(kukaRoute.routeBook.Override[i]);
                        kukaRoute.kRC_MOVESLINEARABSOLUTE[i].ACCELERATION = Convert.ToInt16(kukaRoute.routeBook.Accerlerate[i]);
                        kukaRoute.kRC_MOVESLINEARABSOLUTE[i].BUFFERMODE = 2;
                        kukaRoute.kRC_MOVESLINEARABSOLUTE[i].EXECUTECMD = false;
                        break;
                    case 3:
                        kukaRoute.e6POS[i] = new E6POS
                        {
                            X = kukaRoute.routeBook.X[i],
                            Y = kukaRoute.routeBook.Y[i],
                            Z = kukaRoute.routeBook.Z[i],
                            A = kukaRoute.routeBook.A[i],
                            B = kukaRoute.routeBook.B[i],
                            C = kukaRoute.routeBook.C[i],
                            STATUS = kukaRoute.routeBook.S[i],
                            TURN = kukaRoute.routeBook.T[i]
                        };
                        kukaRoute.kRC_MOVEDIRECTABSOLUTE[i] = new KRC_MOVEDIRECTABSOLUTE();
                        kukaRoute.kRC_MOVEDIRECTABSOLUTE[i].AXISGROUPIDX = GroupId;
                        kukaRoute.kRC_MOVEDIRECTABSOLUTE[i].ACTPOSITION = kukaRoute.e6POS[i];
                        kukaRoute.kRC_MOVEDIRECTABSOLUTE[i].COORDINATESYSTEM = kukaRoute.cOORDSYS[i];
                        if (kukaRoute.routeBook.ContinueMove[i] && i > 0 && i < kukaRoute.routeBook.PointNumber - 1)
                        {
                            if (kukaRoute.routeBook.MovingMode[i + 1] == 1 || kukaRoute.routeBook.MovingMode[i + 1] == 2)
                                kukaRoute.kRC_MOVEDIRECTABSOLUTE[i].APPROXIMATE = pTP_CP_APO;
                            else
                                kukaRoute.kRC_MOVEDIRECTABSOLUTE[i].APPROXIMATE = pTP_APO;
                        }
                        else
                            kukaRoute.kRC_MOVEDIRECTABSOLUTE[i].APPROXIMATE = stop_APO;
                        kukaRoute.kRC_MOVEDIRECTABSOLUTE[i].VELOCITY = Convert.ToInt16(kukaRoute.routeBook.Override[i]);
                        kukaRoute.kRC_MOVEDIRECTABSOLUTE[i].ACCELERATION = Convert.ToInt16(kukaRoute.routeBook.Accerlerate[i]);
                        kukaRoute.kRC_MOVEDIRECTABSOLUTE[i].BUFFERMODE = 2;
                        kukaRoute.kRC_MOVEDIRECTABSOLUTE[i].EXECUTECMD = false;
                        break;
                    case 4:
                        kukaRoute.e6AXIS[i] = new E6AXIS
                        {
                            A1 = kukaRoute.routeBook.X[i],
                            A2 = kukaRoute.routeBook.Y[i],
                            A3 = kukaRoute.routeBook.Z[i],
                            A4 = kukaRoute.routeBook.A[i],
                            A5 = kukaRoute.routeBook.B[i],
                            A6 = kukaRoute.routeBook.C[i]
                        };
                        kukaRoute.kRC_MOVEAXISABSOLUTE[i] = new KRC_MOVEAXISABSOLUTE();
                        kukaRoute.kRC_MOVEAXISABSOLUTE[i].AXISGROUPIDX = GroupId;
                        kukaRoute.kRC_MOVEAXISABSOLUTE[i].AXISPOSITION = kukaRoute.e6AXIS[i];
                        if (kukaRoute.routeBook.ContinueMove[i] && i > 0 && i < kukaRoute.routeBook.PointNumber - 1)
                        {
                            if (kukaRoute.routeBook.MovingMode[i + 1] == 1 || kukaRoute.routeBook.MovingMode[i + 1] == 2)
                                kukaRoute.kRC_MOVEAXISABSOLUTE[i].APPROXIMATE = pTP_CP_APO;
                            else
                                kukaRoute.kRC_MOVEAXISABSOLUTE[i].APPROXIMATE = pTP_APO;
                        }
                        else
                            kukaRoute.kRC_MOVEAXISABSOLUTE[i].APPROXIMATE = stop_APO;
                        kukaRoute.kRC_MOVEAXISABSOLUTE[i].VELOCITY = Convert.ToInt16(kukaRoute.routeBook.Override[i]);
                        kukaRoute.kRC_MOVEAXISABSOLUTE[i].ACCELERATION = Convert.ToInt16(kukaRoute.routeBook.Accerlerate[i]);
                        kukaRoute.kRC_MOVEAXISABSOLUTE[i].BUFFERMODE = 2;
                        kukaRoute.kRC_MOVEAXISABSOLUTE[i].EXECUTECMD = false;
                        break;
                    default:
                        kukaRoute.kRC_MOVELINEARABSOLUTE[i] = new KRC_MOVELINEARABSOLUTE();
                        kukaRoute.kRC_MOVELINEARABSOLUTE[i].AXISGROUPIDX = GroupId;
                        kukaRoute.kRC_MOVELINEARABSOLUTE[i].ACTPOSITION = kukaRoute.e6POS[i];
                        kukaRoute.kRC_MOVELINEARABSOLUTE[i].COORDINATESYSTEM = kukaRoute.cOORDSYS[i];
                        if (kukaRoute.routeBook.ContinueMove[i] && i > 0 && i < kukaRoute.routeBook.PointNumber - 1)
                            kukaRoute.kRC_MOVELINEARABSOLUTE[i].APPROXIMATE = cP_APO;
                        else
                            kukaRoute.kRC_MOVELINEARABSOLUTE[i].APPROXIMATE = stop_APO;
                        kukaRoute.kRC_MOVELINEARABSOLUTE[i].VELOCITY = Convert.ToInt16(kukaRoute.routeBook.Override[i]);
                        kukaRoute.kRC_MOVELINEARABSOLUTE[i].ACCELERATION = Convert.ToInt16(kukaRoute.routeBook.Accerlerate[i]);
                        kukaRoute.kRC_MOVELINEARABSOLUTE[i].BUFFERMODE = 2;
                        kukaRoute.kRC_MOVELINEARABSOLUTE[i].EXECUTECMD = false;
                        break;
                }
            }

            for (int i = 0; i < kukaRoute.routeBook.DoutNumber; i++)
            {
                kukaRoute.kRC_SETDISTANCETRIGGER[i] = new KRC_SETDISTANCETRIGGER();
                kukaRoute.kRC_SETDISTANCETRIGGER[i].AXISGROUPIDX = GroupId;
                kukaRoute.kRC_SETDISTANCETRIGGER[i].DISTANCE = 0;
                kukaRoute.kRC_SETDISTANCETRIGGER[i].OUTPUT = Convert.ToInt16(Math.Abs(kukaRoute.routeBook.DOutMode[i]));
                if (kukaRoute.routeBook.DOutMode[i] > 0)
                    kukaRoute.kRC_SETDISTANCETRIGGER[i].VALUE = true;
                else
                    kukaRoute.kRC_SETDISTANCETRIGGER[i].VALUE = false;
                kukaRoute.kRC_SETDISTANCETRIGGER[i].BUFFERMODE = 2;
                kukaRoute.kRC_SETDISTANCETRIGGER[i].EXECUTECMD = false;
            }

            return kukaRoute;
        }

        public int Connect(string _ipSend, string _ipReceive, string _portSend, string _portReceive)
        {
            try
            {

                IPAddress _ipAddressIN, _ipAddressOUT;
                SocketSend = new UdpClient();
                
                if (IPAddress.TryParse(_ipReceive, out _ipAddressOUT))
                {
                    ReceiveEndPoint = new IPEndPoint(_ipAddressOUT, Convert.ToInt32(_portReceive));
                    SocketReceive = new UdpClient(ReceiveEndPoint) { Client = { ReceiveTimeout = 10 } };

                    if (IPAddress.TryParse(_ipSend, out _ipAddressIN))
                    {
                        SendEndPoint = new IPEndPoint(_ipAddressIN, Convert.ToInt32(_portSend));
                        SocketSend.Connect(SendEndPoint);

                        FlagPower = new ConcurrentQueue<bool>();
                        FlagReset = new ConcurrentQueue<bool>();
                        FlagResetAlarm = new ConcurrentQueue<bool>();

                        FlagPower.Enqueue(true);
                        FlagReset.Enqueue(true);
                        FlagResetAlarm.Enqueue(true);
                        CycleType.Enqueue(1);

                        MerryGoRound.RunWorkerAsync();

                        return 0;
                    }
                    else
                        return 1;
                }
                else
                    return 1;
            }
            catch (Exception ex)
            {
                MerryGoRound.CancelAsync();
                SocketSend.Close();
                SocketReceive.Close();
                SocketSend = new UdpClient();
                SocketReceive = new UdpClient();
                return 1;
            }
        }

        public int Close()
        {
            try
            {
                FlagPower.Enqueue(false);
                SpinWait.SpinUntil(() => false, 100);
                SocketSend.Close();
                SocketReceive.Close();
                SocketSend = new UdpClient();
                SocketReceive = new UdpClient();
                return 0;
            }
            catch (Exception ex)
            { return 1; }
        }

        public int ResetAlarm()
        {
            ConnectState = "unstable";
            FlagReset.Enqueue(true);
            return 0;
        }

        public int RunInverse(RefAXIS _refAXIS)
        {
            FinishCount = 0;
            currentAXIS = null;
            currentAXIS = _refAXIS;
            currentAXIS.counter = 0;
            CycleType.Enqueue(5);
            return 0;
        }

        public int RunForward(RefPOS _refPOS)
        {
            FinishCount = 0;
            currentPOS = null;
            currentPOS = _refPOS;
            currentPOS.counter = 0;
            CycleType.Enqueue(7);
            return 0;
        }

        public int RunProgram(KukaRoute _kukaRoute)
        {
            FinishCount = 0;
            currentRoute = _kukaRoute;
            currentRoute.routeBook.PointCount = 0;
            CycleType.Enqueue(2);
            return 0;
        }

        private int UploadAXIS()
        {
            try
            {
                currentAXIS.kRC_INVERSE[currentAXIS.counter].START_AXIS = currentAXIS.refAXIS;
                currentAXIS.kRC_INVERSE[currentAXIS.counter].ACTPOSITION = currentAXIS.e6POS[currentAXIS.counter];
                currentAXIS.kRC_INVERSE[currentAXIS.counter].OnCycle();
                return 0;
            }
            catch
            { return 1; }
        }

        private int UploadPOS()
        {
            try
            {
                for (int i = 0; i < currentPOS.count; i++)
                {
                    currentPOS.kRC_FORWARD[i].AXIS_VALUES = currentPOS.e6AXIS[i];
                    currentPOS.kRC_FORWARD[i].OnCycle();
                }
                return 0;
            }
            catch
            { return 1; }
        }

        private int Upload2controller()
        {
            kRC_WRITESYSVAR.BCONTINUE = false;
            kRC_WRITESYSVAR.EXECUTECMD = false;
            kRC_WRITESYSVAR.OnCycle();

            kRC_SETADVANCE.EXECUTECMD = false;
            kRC_SETADVANCE.OnCycle();

            try
            {
                currentRoute.routeBook.PointCount = 0; currentRoute.routeBook.DoutCount = 0;
                for (currentRoute.routeBook.CommandCount = 0; currentRoute.routeBook.CommandCount < currentRoute.routeBook.PointNumber + currentRoute.routeBook.DoutNumber - 1; currentRoute.routeBook.CommandCount++)
                {
                    switch (currentRoute.routeBook.ProcessQueue[currentRoute.routeBook.CommandCount])
                    {
                        case 1:
                            switch (currentRoute.routeBook.MovingMode[currentRoute.routeBook.PointCount])
                            {
                                case 1:
                                    currentRoute.kRC_MOVELINEARABSOLUTE[currentRoute.routeBook.PointCount].ACTPOSITION = currentRoute.e6POS[currentRoute.routeBook.PointCount];
                                    currentRoute.kRC_MOVELINEARABSOLUTE[currentRoute.routeBook.PointCount].OnCycle();
                                    break;
                                case 2:
                                    currentRoute.kRC_MOVESLINEARABSOLUTE[currentRoute.routeBook.PointCount].ACTPOSITION = currentRoute.e6POS[currentRoute.routeBook.PointCount];
                                    currentRoute.kRC_MOVESLINEARABSOLUTE[currentRoute.routeBook.PointCount].OnCycle();
                                    break;
                                case 3:
                                    currentRoute.kRC_MOVEDIRECTABSOLUTE[currentRoute.routeBook.PointCount].ACTPOSITION = currentRoute.e6POS[currentRoute.routeBook.PointCount];
                                    currentRoute.kRC_MOVEDIRECTABSOLUTE[currentRoute.routeBook.PointCount].OnCycle();
                                    break;
                                case 4:
                                    currentRoute.kRC_MOVEAXISABSOLUTE[currentRoute.routeBook.PointCount].AXISPOSITION = currentRoute.e6AXIS[currentRoute.routeBook.PointCount];
                                    currentRoute.kRC_MOVEAXISABSOLUTE[currentRoute.routeBook.PointCount].OnCycle();
                                    break;
                                default:
                                    currentRoute.kRC_MOVELINEARABSOLUTE[currentRoute.routeBook.PointCount].ACTPOSITION = currentRoute.e6POS[currentRoute.routeBook.PointCount];
                                    currentRoute.kRC_MOVELINEARABSOLUTE[currentRoute.routeBook.PointCount].OnCycle();
                                    break;
                            }
                            currentRoute.routeBook.PointCount++;
                            break;
                        case 2:
                            currentRoute.kRC_SETDISTANCETRIGGER[currentRoute.routeBook.DoutCount].OnCycle();
                            currentRoute.routeBook.DoutCount++;
                            break;
                    }
                }
                return 0;
            }
            catch (Exception ex)
            { return 1; }
        }

        private int RunInverse()
        {
            try
            {
                currentAXIS.kRC_INVERSE[currentAXIS.counter].EXECUTECMD = currentAXIS.ActiveMode[currentAXIS.counter];
                currentAXIS.kRC_INVERSE[currentAXIS.counter].OnCycle();
                if (currentAXIS.kRC_INVERSE[currentAXIS.counter].DONE)
                {
                    currentAXIS.ActiveMode[currentAXIS.counter] = false;
                    float[] tmp1 = new float[6];
                    float[] tmp2 = new float[6];
                    tmp1[0] = currentAXIS.kRC_INVERSE[currentAXIS.counter].AXISPOSITION.A1;
                    tmp1[1] = currentAXIS.kRC_INVERSE[currentAXIS.counter].AXISPOSITION.A2;
                    tmp1[2] = currentAXIS.kRC_INVERSE[currentAXIS.counter].AXISPOSITION.A3;
                    tmp1[3] = currentAXIS.kRC_INVERSE[currentAXIS.counter].AXISPOSITION.A4;
                    tmp1[4] = currentAXIS.kRC_INVERSE[currentAXIS.counter].AXISPOSITION.A5;
                    tmp1[5] = currentAXIS.kRC_INVERSE[currentAXIS.counter].AXISPOSITION.A6;
                    GCHandle gCHandle = GCHandle.Alloc(tmp2, GCHandleType.Pinned);
                    Marshal.Copy(tmp1, 0, gCHandle.AddrOfPinnedObject(), tmp1.Length);
                    gCHandle.Free();
                    currentAXIS.refAXIS.A1 = tmp2[0];
                    currentAXIS.refAXIS.A2 = tmp2[1];
                    currentAXIS.refAXIS.A3 = tmp2[2];
                    currentAXIS.refAXIS.A4 = tmp2[3];
                    currentAXIS.refAXIS.A5 = tmp2[4];
                    currentAXIS.refAXIS.A6 = tmp2[5];
                    currentAXIS.queue.Enqueue(tmp2);
                    currentAXIS.kRC_INVERSE[currentAXIS.counter].EXECUTECMD = currentAXIS.ActiveMode[currentAXIS.counter];
                    currentAXIS.kRC_INVERSE[currentAXIS.counter].OnCycle();
                    currentAXIS.counter++;
                }
                return 0;
            }
            catch
            { return 1; }
        }

        private int RunForward()
        {
            try
            {
                for (int i = 0; i < currentPOS.count; i++)
                {
                    currentPOS.kRC_FORWARD[i].EXECUTECMD = currentPOS.ActiveMode[i];
                    currentPOS.kRC_FORWARD[i].OnCycle();
                    if (currentPOS.kRC_FORWARD[i].DONE)
                    {
                        currentPOS.ActiveMode[i] = false;
                        float[] tmp1 = new float[8];
                        float[] tmp2 = new float[8];
                        tmp1[0] = currentPOS.kRC_FORWARD[i].ACTPOSITION.X;
                        tmp1[1] = currentPOS.kRC_FORWARD[i].ACTPOSITION.Y;
                        tmp1[2] = currentPOS.kRC_FORWARD[i].ACTPOSITION.Z;
                        tmp1[3] = currentPOS.kRC_FORWARD[i].ACTPOSITION.A;
                        tmp1[4] = currentPOS.kRC_FORWARD[i].ACTPOSITION.B;
                        tmp1[5] = currentPOS.kRC_FORWARD[i].ACTPOSITION.C;
                        tmp1[6] = (float)currentPOS.kRC_FORWARD[i].ACTPOSITION.STATUS;
                        tmp1[7] = (float)currentPOS.kRC_FORWARD[i].ACTPOSITION.TURN;
                        GCHandle gCHandle = GCHandle.Alloc(tmp2, GCHandleType.Pinned);
                        Marshal.Copy(tmp1, 0, gCHandle.AddrOfPinnedObject(), tmp1.Length);
                        gCHandle.Free();
                        currentPOS.queue.Enqueue(tmp2);
                        FinishCount++;
                    }
                }
                if (FinishCount == currentPOS.count)
                    return 0;
                else
                    return 1;
            }
            catch
            { return 0; }
        }

        private int RunProgram()
        {
            kRC_WRITESYSVAR.BCONTINUE = true;
            kRC_WRITESYSVAR.EXECUTECMD = true;
            kRC_WRITESYSVAR.OnCycle();

            kRC_SETADVANCE.EXECUTECMD = true;
            kRC_SETADVANCE.OnCycle();

            try
            {
                #region queue
                currentRoute.routeBook.PointCount = 0; currentRoute.routeBook.DoutCount = 0;
                for (currentRoute.routeBook.CommandCount = 0; currentRoute.routeBook.CommandCount < currentRoute.routeBook.PointNumber + currentRoute.routeBook.DoutNumber; currentRoute.routeBook.CommandCount++)
                {
                    switch (currentRoute.routeBook.ProcessQueue[currentRoute.routeBook.CommandCount])
                    {
                        case 1:
                            switch (currentRoute.routeBook.MovingMode[currentRoute.routeBook.PointCount])
                            {
                                case 1:
                                    currentRoute.kRC_MOVELINEARABSOLUTE[currentRoute.routeBook.PointCount].EXECUTECMD = currentRoute.ActiveMode[currentRoute.routeBook.CommandCount];
                                    currentRoute.kRC_MOVELINEARABSOLUTE[currentRoute.routeBook.PointCount].OnCycle();
                                    if (currentRoute.kRC_MOVELINEARABSOLUTE[currentRoute.routeBook.PointCount].DONE)
                                    {
                                        currentRoute.ActiveMode[currentRoute.routeBook.CommandCount] = false;
                                        FinishCount++;
                                    }
                                    break;
                                case 2:
                                    currentRoute.kRC_MOVESLINEARABSOLUTE[currentRoute.routeBook.PointCount].EXECUTECMD = currentRoute.ActiveMode[currentRoute.routeBook.CommandCount];
                                    currentRoute.kRC_MOVESLINEARABSOLUTE[currentRoute.routeBook.PointCount].OnCycle();
                                    if (currentRoute.kRC_MOVESLINEARABSOLUTE[currentRoute.routeBook.PointCount].DONE)
                                    {
                                        currentRoute.ActiveMode[currentRoute.routeBook.CommandCount] = false;
                                        FinishCount++;
                                    }
                                    break;
                                case 3:
                                    currentRoute.kRC_MOVEDIRECTABSOLUTE[currentRoute.routeBook.PointCount].EXECUTECMD = currentRoute.ActiveMode[currentRoute.routeBook.CommandCount];
                                    currentRoute.kRC_MOVEDIRECTABSOLUTE[currentRoute.routeBook.PointCount].OnCycle();
                                    if (currentRoute.kRC_MOVEDIRECTABSOLUTE[currentRoute.routeBook.PointCount].DONE)
                                    {
                                        currentRoute.ActiveMode[currentRoute.routeBook.CommandCount] = false;
                                        FinishCount++;
                                    }
                                    break;
                                case 4:
                                    currentRoute.kRC_MOVEAXISABSOLUTE[currentRoute.routeBook.PointCount].EXECUTECMD = currentRoute.ActiveMode[currentRoute.routeBook.CommandCount];
                                    currentRoute.kRC_MOVEAXISABSOLUTE[currentRoute.routeBook.PointCount].OnCycle();
                                    if (currentRoute.kRC_MOVEAXISABSOLUTE[currentRoute.routeBook.PointCount].DONE)
                                    {
                                        currentRoute.ActiveMode[currentRoute.routeBook.CommandCount] = false;
                                        FinishCount++;
                                    }
                                    break;
                                default:
                                    currentRoute.kRC_MOVELINEARABSOLUTE[currentRoute.routeBook.PointCount].EXECUTECMD = currentRoute.ActiveMode[currentRoute.routeBook.CommandCount];
                                    currentRoute.kRC_MOVELINEARABSOLUTE[currentRoute.routeBook.PointCount].OnCycle();
                                    if (currentRoute.kRC_MOVELINEARABSOLUTE[currentRoute.routeBook.PointCount].DONE)
                                    {
                                        currentRoute.ActiveMode[currentRoute.routeBook.CommandCount] = false;
                                        FinishCount++;
                                    }
                                    break;
                            }
                            currentRoute.routeBook.PointCount++;
                            break;
                        case 2:
                            currentRoute.kRC_SETDISTANCETRIGGER[currentRoute.routeBook.DoutCount].EXECUTECMD = currentRoute.ActiveMode[currentRoute.routeBook.CommandCount];
                            currentRoute.kRC_SETDISTANCETRIGGER[currentRoute.routeBook.DoutCount].OnCycle();
                            if (currentRoute.kRC_SETDISTANCETRIGGER[currentRoute.routeBook.DoutCount].DONE)
                            {
                                currentRoute.ActiveMode[currentRoute.routeBook.CommandCount] = false;
                                FinishCount++;
                            }
                            currentRoute.routeBook.DoutCount++;
                            break;
                    }
                }
                #endregion

                if (FinishCount == currentRoute.routeBook.PointNumber + currentRoute.routeBook.DoutNumber)
                    return 0;
                else
                    return 1;
            }
            catch (Exception ex)
            {
                string aaa = ex.ToString();
                return 0;
            }
        }

        private void DisposeProgram()
        {
            kRC_WRITESYSVAR.BCONTINUE = false;
            kRC_WRITESYSVAR.EXECUTECMD = false;
            kRC_WRITESYSVAR.OnCycle();

            kRC_SETADVANCE.EXECUTECMD = false;
            kRC_SETADVANCE.OnCycle();

            currentRoute.routeBook.PointCount = 0; currentRoute.routeBook.DoutCount = 0;
            for (currentRoute.routeBook.CommandCount = 0; currentRoute.routeBook.CommandCount < currentRoute.routeBook.PointNumber + currentRoute.routeBook.DoutNumber; currentRoute.routeBook.CommandCount++)
            {
                currentRoute.ActiveMode[currentRoute.routeBook.CommandCount] = false;
                switch (currentRoute.routeBook.ProcessQueue[currentRoute.routeBook.CommandCount])
                {
                    case 1:
                        switch (currentRoute.routeBook.MovingMode[currentRoute.routeBook.PointCount])
                        {
                            case 1:
                                currentRoute.kRC_MOVELINEARABSOLUTE[currentRoute.routeBook.PointCount].EXECUTECMD = currentRoute.ActiveMode[currentRoute.routeBook.CommandCount];
                                currentRoute.kRC_MOVELINEARABSOLUTE[currentRoute.routeBook.PointCount].OnCycle();
                                break;
                            case 2:
                                currentRoute.kRC_MOVESLINEARABSOLUTE[currentRoute.routeBook.PointCount].EXECUTECMD = currentRoute.ActiveMode[currentRoute.routeBook.CommandCount];
                                currentRoute.kRC_MOVESLINEARABSOLUTE[currentRoute.routeBook.PointCount].OnCycle();
                                break;
                            case 3:
                                currentRoute.kRC_MOVEDIRECTABSOLUTE[currentRoute.routeBook.PointCount].EXECUTECMD = currentRoute.ActiveMode[currentRoute.routeBook.CommandCount];
                                currentRoute.kRC_MOVEDIRECTABSOLUTE[currentRoute.routeBook.PointCount].OnCycle();
                                break;
                            case 4:
                                currentRoute.kRC_MOVEAXISABSOLUTE[currentRoute.routeBook.PointCount].EXECUTECMD = currentRoute.ActiveMode[currentRoute.routeBook.CommandCount];
                                currentRoute.kRC_MOVEAXISABSOLUTE[currentRoute.routeBook.PointCount].OnCycle();
                                break;
                            default:
                                currentRoute.kRC_MOVELINEARABSOLUTE[currentRoute.routeBook.PointCount].EXECUTECMD = currentRoute.ActiveMode[currentRoute.routeBook.CommandCount];
                                currentRoute.kRC_MOVELINEARABSOLUTE[currentRoute.routeBook.PointCount].OnCycle();
                                break;
                        }
                        currentRoute.routeBook.PointCount++;
                        break;
                    default:
                        currentRoute.kRC_SETDISTANCETRIGGER[currentRoute.routeBook.DoutCount].EXECUTECMD = currentRoute.ActiveMode[currentRoute.routeBook.CommandCount];
                        currentRoute.kRC_SETDISTANCETRIGGER[currentRoute.routeBook.DoutCount].OnCycle();
                        currentRoute.routeBook.DoutCount++;
                        break;
                }
            }
        }

        private void ReadAxisGroup()
        {
            byte[] buffer = null;

            try
            {
                if (SocketReceive.Available > 0)
                {
                    buffer = SocketReceive.Receive(ref ReceiveEndPoint);
                    ReceiveTimeout = 0;
                }
                else
                {
                    ReceiveTimeout++;
                    if (ReceiveTimeout > 50)
                    {
                        ReceiveTimeout = 0;
                        throw new Exception("timeout");
                    }
                }
            }
            catch (SocketException ex)
            { return; }

            if (buffer != null && buffer.Length >= 256)
                KRCInput = buffer;

            kRC_READAXISGROUP.KRC4_INPUT = KRCInput;
            kRC_READAXISGROUP.AXISGROUPIDX = GroupId;
            kRC_READAXISGROUP.OnCycle();

            if (kRC_READAXISGROUP.ERROR)
                return;

            kRC_DIAG.AXISGROUPIDX = GroupId;
            kRC_DIAG.OnCycle();

            bool _flagResetAlarm;

            if (FlagResetAlarm.TryPeek(out _flagResetAlarm))
                FlagResetAlarm.TryDequeue(out _flagResetAlarm);

            kRC_ERROR.AXISGROUPIDX = GroupId;
            kRC_ERROR.MESSAGERESET = _flagResetAlarm;
            kRC_ERROR.OnCycle();

            ErrorId = kRC_ERROR.ERRORID;
        }

        private void WriteAxisGroup()
        {
            kRC_WRITEAXISGROUP.AXISGROUPIDX = GroupId;
            kRC_WRITEAXISGROUP.KRC4_OUTPUT = KRCOutput;
            kRC_WRITEAXISGROUP.OnCycle();

            SocketSend.Send(KRCOutput, KRCOutput.Length);
        }

        private void InitialKRC(bool _reset)
        {
            GLOBAL.KRC_AXISGROUPREFARR[GroupId].HEARTBEATTO = 2000;
            GLOBAL.KRC_AXISGROUPREFARR[GroupId].DEF_VEL_CP = 2;
            GLOBAL.KRC_AXISGROUPREFARR[GroupId].KRCSTATE.POSACTVALID = true;

            GLOBAL.KRC_AXISGROUPREFARR[GroupId].CMDSTATE.CMDDATARETURN[5] = 3;

            kRC_INITIALIZE.AXISGROUPIDX = GroupId;
            kRC_INITIALIZE.OnCycle();

            kRC_AUTOSTART.AXISGROUPIDX = GroupId;
            kRC_AUTOSTART.EXECUTERESET = _reset;
            kRC_AUTOSTART.OnCycle();

            kRC_AUTOMATICEXTERNAL.AXISGROUPIDX = GroupId;
            kRC_AUTOMATICEXTERNAL.MOVE_ENABLE = true;
            kRC_AUTOMATICEXTERNAL.DRIVES_ON = false;
            kRC_AUTOMATICEXTERNAL.DRIVES_OFF = false;
            kRC_AUTOMATICEXTERNAL.RESET = _reset;
            kRC_AUTOMATICEXTERNAL.ENABLE_T1 = false;
            kRC_AUTOMATICEXTERNAL.ENABLE_T2 = true;
            kRC_AUTOMATICEXTERNAL.ENABLE_AUT = false;
            kRC_AUTOMATICEXTERNAL.ENABLE_EXT = true;
            kRC_AUTOMATICEXTERNAL.OnCycle();

            kRC_SETOVERRIDE.AXISGROUPIDX = GroupId;
            if (_reset)
                kRC_SETOVERRIDE.OVERRIDE = 5;
            kRC_SETOVERRIDE.OnCycle();

            kRC_READACTUALPOSITION.AXISGROUPIDX = GroupId;
            kRC_READACTUALPOSITION.OnCycle();

        }

        private string CheckKRC(bool _reset, ref int _resetcount)
        {
            if (!_reset && _resetcount == 0) return "stable";

            if (kRC_AUTOMATICEXTERNAL.RC_RDY1 && kRC_AUTOMATICEXTERNAL.PRO_ACT)
            {
                FlagReset.Enqueue(false);
                _resetcount = 0;
                return "stable";
            }
            else
            {
                FlagResetAlarm.Enqueue(true);

                _resetcount++;

                if (_resetcount > 50)
                {
                    _resetcount = -50;
                    FlagReset.Enqueue(false);
                }
                else if (_resetcount == -1)
                {
                    _resetcount = 1;
                    FlagReset.Enqueue(true);
                }
                return "unstable";
            }
        }
    }
}
