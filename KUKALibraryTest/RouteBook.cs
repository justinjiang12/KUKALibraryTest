using System;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace RouteButler_KUKA
{
    [Serializable]
    public class RouteBook_KUKA
    {
        public int StartPosture { get; set; }

        public int CommandCount { get; set; }
        public int PointCount { get; set; }
        public int DoutCount { get; set; }

        public string FileName;
        public int PointNumber { get; set; }
        public int DoutNumber { get; set; }
        public int[] MovingMode { get; set; }//1:linear 2:slinear 3:direct 4:axis
        public int[] DOutMode { get; set; }//+:ON -:OFF
        public int[] ProcessQueue { get; set; }//1:move 2:dout
        public int[] Override { get; set; }//0~100
        public int[] Accerlerate { get; set; }//0~100
        public bool[] ContinueMove { get; set; }//true:continue false:p2p
        public float[] X { get; set; }
        public float[] Y { get; set; }
        public float[] Z { get; set; }
        public float[] A { get; set; }
        public float[] B { get; set; }
        public float[] C { get; set; }
        public short[] S { get; set; }
        public short[] T { get; set; }
        public float[] Workspace { get; set; }//1~32
        public float[] Tool { get; set; }//1~16

        public RouteBook_KUKA() { }

        public RouteBook_KUKA(string _fileName = "None", int _pointNumber = 1, int _doutNumber = 1, int _startPosture = 0)
        {
            StartPosture = _startPosture;

            CommandCount = 0;
            PointCount = 0;
            DoutCount = 0;

            FileName = _fileName;
            PointNumber = _pointNumber;
            DoutNumber = _doutNumber;

            ProcessQueue = new int[_pointNumber + _doutNumber];
            MovingMode = new int[_pointNumber];
            DOutMode = new int[_doutNumber];

            Override = new int[_pointNumber];
            Accerlerate = new int[_pointNumber];

            ContinueMove = new bool[_pointNumber];

            X = new float[_pointNumber];
            Y = new float[_pointNumber];
            Z = new float[_pointNumber];
            A = new float[_pointNumber];
            B = new float[_pointNumber];
            C = new float[_pointNumber];
            S = new short[_pointNumber];
            T = new short[_pointNumber];
            Workspace = new float[_pointNumber];
            Tool = new float[_pointNumber];
        }
    }

    public class Librarian_KUKA
    {
        public void SaveFile(RouteBook_KUKA _routeBook, string _filepath, string _filename)
        {
            string path = _filepath + "\\" + "KUKA_" + _filename + ".txt";
            using (FileStream oFileStream = new FileStream(path, FileMode.Create))
            {
                BinaryFormatter binaryFormatter = new BinaryFormatter();
                binaryFormatter.Serialize(oFileStream, _routeBook);
                oFileStream.Flush();
                oFileStream.Close();
                oFileStream.Dispose();
            };
        }

        public RouteBook_KUKA LoadFile(string _filepath, string _filename)
        {
            string path = _filepath + "\\" + "KUKA_" + _filename + ".txt";
            
            using (FileStream oFileStream = new FileStream(path, FileMode.Open))
            {
                BinaryFormatter binaryFormatter = new BinaryFormatter();
                RouteBook_KUKA pathStructure = (RouteBook_KUKA)binaryFormatter.Deserialize(oFileStream);
                oFileStream.Flush();
                oFileStream.Close();
                oFileStream.Dispose();
                return pathStructure;
            }
        }
    }
}
