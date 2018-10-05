using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MissionPlanner
{
        public class ElistairClass
        {

            string inMsg = "00!00!00!00!00!00	!0!0.0	!000	!0.00	!00000	!10";
            //string inMsg = "00!00!00!00!60!24	!0!0.1	!0	!0.00	!00000	!";

            string[] valueList;

            public ElistairClass()
            {
                valueList = inMsg.Split('!');
            }

            public string Message
            {
                get { return inMsg; }
                set
                {
                    inMsg = value == "" ? "00!00!00!00!00!00	!0!0.0	!000	!0.00	!00000	!00" : value;
                    inMsg = Regex.Replace(inMsg, @"<.*?>", string.Empty);
                    valueList = inMsg.Split('!');
                }
            }

            public string Time { get { return valueList[0] + ":" + valueList[1] + ":" + valueList[2] + ":" + valueList[3] + ":" + valueList[4]; } }
            public int Temperature { get { return Int32.Parse(valueList[5]); } }
            public int Power { get { return Int32.Parse(valueList[6]); } }
            public double CableSpeed { get { return Double.Parse(valueList[9].Trim().Replace('.', ',')); } }
            public int TorquePercent { get { return Int32.Parse(valueList[8]); } }
            public double CableLength { get { return Double.Parse(valueList[7].Trim().Replace('.', ',')); } }
            public int Torque { get { return Int32.Parse(valueList[11] == "" ? "0" : valueList[11]); } }
            public string AnyInfo { get { return valueList[10]; } }

        }
}
