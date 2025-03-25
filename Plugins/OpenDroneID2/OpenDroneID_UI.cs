using System;
using System.Linq;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Windows.Forms;
using MissionPlanner.Comms;
using MissionPlanner.Utilities;
using System.Drawing;
using System.Diagnostics;
using static MissionPlanner.Utilities.LTM;
using System.Runtime.InteropServices;
using System.Collections.Generic;


namespace MissionPlanner.Controls
{
    public partial class OpenDroneID_UI : UserControl
    {
        static OpenDroneID_UI Instance;

        static MAVLink.mavlink_open_drone_id_arm_status_t odid_arm_status;
        private bool hasODID = false;
        
        private bool gpsHasSBAS = false;

        private Stopwatch last_odid_msg = new Stopwatch();
        PointLatLngAlt gotolocation = new PointLatLngAlt();

        private bool _odid_arm_msg, _uas_id, _gcs_gps, _odid_arm_status; 

        private const int ODID_ARM_MESSAGE_TIMEOUT = 5000;
        private OpenDroneID_Backend myDID = new OpenDroneID_Backend();
        private OpenDroneID_Map_Status host_map_status = new OpenDroneID_Map_Status();

        private Plugin.PluginHost _host = null;

        private int _mySYS = 0; 

        private int _last_odid_error_agg;

        System.Threading.Thread _thread_odid;
        static bool threadrun = false;
        DateTime _last_time_1 = DateTime.Now;
        float _update_rate_hz_1 = 10.0f; // 10 hz
        DateTime _last_time_2 = DateTime.Now;
        float _update_rate_hz_2 = 1.0f; // 1 hz

        bool dev_mode_rm = false;

        public OpenDroneID_UI()
        {
            Instance = this;
            
            

            InitializeComponent();

            CMB_op_id_type.DisplayMember = "Value";
            CMB_op_id_type.ValueMember = "Key";
            CMB_op_id_type.DataSource = System.Enum.GetValues(typeof(MAVLink.MAV_ODID_OPERATOR_ID_TYPE));

            CMB_uas_id_type.DisplayMember = "Value";
            CMB_uas_id_type.ValueMember = "Key";
            CMB_uas_id_type.DataSource = System.Enum.GetValues(typeof(MAVLink.MAV_ODID_ID_TYPE));

            CMB_uas_type.DisplayMember = "Value";
            CMB_uas_type.ValueMember = "Key";
            CMB_uas_type.DataSource = System.Enum.GetValues(typeof(MAVLink.MAV_ODID_UA_TYPE));

            CMB_self_id_type.DisplayMember = "Value";
            CMB_self_id_type.ValueMember = "Key";
            CMB_self_id_type.DataSource = System.Enum.GetValues(typeof(MAVLink.MAV_ODID_DESC_TYPE));

            CMB_EU_Category.DisplayMember = "Value";
            CMB_EU_Category.ValueMember = "Key";
            CMB_EU_Category.DataSource = System.Enum.GetValues(typeof(MAVLink.MAV_ODID_CATEGORY_EU));

            CMB_EU_Class.DisplayMember = "Value";
            CMB_EU_Class.ValueMember = "Key";
            CMB_EU_Class.DataSource = System.Enum.GetValues(typeof(MAVLink.MAV_ODID_CLASS_EU));

            myODID_Status._parent_ODID = this;

            start();
        }

        public void start()
        {
            //Console.WriteLine();
            _thread_odid = new System.Threading.Thread(new System.Threading.ThreadStart(mainloop))
            {
                IsBackground = true,
                Name = "ODID_Thread"
            };
            _thread_odid.Start();
        }

        public void setVer(String msg)
        {
            LBL_version.Text = msg;
        }
        public void setHost(Plugin.PluginHost host)
        {
            _host = host;
        }

        private void start_sub(bool force = false)
        {
            if (_host == null) return;

            if (!force && (_host.comPort.BaseStream == null || !_host.comPort.BaseStream.IsOpen))
            {
                // pass
            }
            else if (_host.comPort.sysidcurrent != _mySYS && _host.comPort.sysidcurrent > 0)
            {
                addStatusMessage("Sub. to ODID ARM_STATUS for SysId: " + _host.comPort.sysidcurrent);
                
                _host.comPort.SubscribeToPacketType(MAVLink.MAVLINK_MSG_ID.OPEN_DRONE_ID_ARM_STATUS, handleODIDArmMSg2, (byte) _host.comPort.sysidcurrent, (byte) MAVLink.MAV_COMPONENT.MAV_COMP_ID_ODID_TXRX_1);
                _host.comPort.SubscribeToPacketType(MAVLink.MAVLINK_MSG_ID.OPEN_DRONE_ID_ARM_STATUS, handleODIDArmMSg2, (byte)_host.comPort.sysidcurrent, (byte)MAVLink.MAV_COMPONENT.MAV_COMP_ID_ODID_TXRX_2);
                _host.comPort.SubscribeToPacketType(MAVLink.MAVLINK_MSG_ID.OPEN_DRONE_ID_ARM_STATUS, handleODIDArmMSg2, (byte)_host.comPort.sysidcurrent, (byte)MAVLink.MAV_COMPONENT.MAV_COMP_ID_ODID_TXRX_3);
                _host.comPort.SubscribeToPacketType(MAVLink.MAVLINK_MSG_ID.OPEN_DRONE_ID_ARM_STATUS, handleODIDArmMSg2, (byte)_host.comPort.sysidcurrent, (byte)MAVLink.MAV_COMPONENT.MAV_COMP_ID_AUTOPILOT1);
                _mySYS =  _host.comPort.sysidcurrent;
                hasODID = false;
                last_odid_msg.Stop();

                //myDID.Stop();
            }
        }

        private bool handleODIDArmMSg2(MAVLink.MAVLinkMessage arg)
        {
            
            odid_arm_status = arg.ToStructure<MAVLink.mavlink_open_drone_id_arm_status_t>();


            // TODO: Check timestamp of ODID message and indicate error
            if (hasODID == true)
            {
                last_odid_msg.Restart();

                int _this_agg = (odid_arm_status.status == 0) ? 0 : odid_arm_status.error.Aggregate(0, (a, b) => a + b);

                if (_this_agg != _last_odid_error_agg)
                {
                    if (odid_arm_status.status != 0) {
                        string s = System.Text.Encoding.ASCII.GetString(odid_arm_status.error);
                        addStatusMessage("Arm Error: " + s.Substring(0, s.IndexOf((char)0)));
                    }
                    else
                        addStatusMessage("Arm Stats: Ready");
                    _last_odid_error_agg = _this_agg;
                }
            }
            else
            {
                try
                {
                    Console.WriteLine("[DRONE_ID] Detected and Starting on System ID: " + _host.comPort.MAV.sysid);
                    addStatusMessage("Detected and Starting on System ID: " + _host.comPort.MAV.sysid);

                    last_odid_msg.Start();
                    if (dev_mode_rm == false)
                        myDID.Start(_host.comPort, arg.sysid, arg.compid);
                    host_map_status.Anchor = AnchorStyles.Right | AnchorStyles.Top;
                    
                    _host.MainForm.Invoke((Action)(() =>
                    {
                        _host.FDGMapControl.Controls.Add(host_map_status);
                        host_map_status.Location = new System.Drawing.Point(_host.FDGMapControl.Width-host_map_status.Width-10, 25);
                        host_map_status._parent_ODID = this;
                    }));
                    host_map_status.Visible = true;

                    addStatusMessage("Double Click Primary status indicator to declare Emergency over ODID.");

                }
                catch
                {
                    Console.WriteLine("Error Initializing ODID Message Handler");
                }

            }
            
            hasODID = true;
            return true; ;
        }

 
 


        private void checkODIDMsgs()
        {
            if (hasODID == false) return;

            // Check Requirements
            _odid_arm_msg = (last_odid_msg.ElapsedMilliseconds < 5000);

            if (_odid_arm_msg == false)
            {
                _odid_arm_status = false;  // can't be valid if it's timed out. 
            } 

            LED_RemoteID_Messages.Color = (_odid_arm_msg==false) ? Color.Red : Color.Green;

            _odid_arm_status = (odid_arm_status.status == 0);
            LED_ArmedError.Color = (_odid_arm_status==false ? Color.Red : Color.Green);


            if (_odid_arm_msg == false)
                TXT_RID_Status_Msg.Text = "Timeout.";
            else
                TXT_RID_Status_Msg.Text = ((odid_arm_status.status == 0) ? "Ready" : System.Text.Encoding.UTF8.GetString(odid_arm_status.error));
                        
        }

        public void setEmergencyFromMap()
        {
            Console.WriteLine("ODID - Pilot Declared an ODID Emergency");
            TXT_self_id_TXT.Text = "Pilot Emergency Status Declared";
            CMB_self_id_type.SelectedIndex = (int)MAVLink.MAV_ODID_DESC_TYPE.EMERGENCY;
            addStatusMessage("Pilot Emergency Status");
        }

        private void checkODID_OK()
        {
            string msg = "";

            if (CMB_self_id_type.SelectedIndex == (int)MAVLink.MAV_ODID_DESC_TYPE.EMERGENCY)
            {
                myODID_Status.setStatusEmergency(TXT_self_id_TXT.Text);
                host_map_status.setStatusEmergency(TXT_self_id_TXT.Text);
                
                return;
            }

            if (_gcs_gps == false)
            {
                msg = "GCS GPS Invalid";

            }
            else if (_odid_arm_msg == false)
            {
                msg = "Remote ID Msg Timeout";

            }
            else if (_odid_arm_status == false)
            {
                msg = "Remote ID ARM Error";

            }            
            else
            {
                myODID_Status.setStatusOK();
                host_map_status.setStatusOK();
                return;
            }

            myODID_Status.setStatusAlert(msg);
            host_map_status.setStatusAlert(msg);


        }

        string opiderror = "";
        //check operator ID
        bool checkOperatorID()
        {
            // exactly 20 characters
            if (txt_UserID.Text.Length != 20)
            {
                opiderror = "Operator ID must be 20 characters long";
                return false;
            }

            // 17th char is a hypen
            if (txt_UserID.Text[16] != '-')
            {
                opiderror = "Operator ID must have a hyphen at position 17";
                return false;
            }

            //first three charactesr are uppercased alpha characters
            if (!char.IsUpper(txt_UserID.Text[0]) || !char.IsUpper(txt_UserID.Text[1]) || !char.IsUpper(txt_UserID.Text[2]))
            {
                opiderror = "First three characters of Operator ID must be upper case alpha characters";
                return false;
            }

            // check if the first three char are valid country codes, using the Countries list
            if (!Countries.Any(a => a.Key == txt_UserID.Text.Substring(0, 3)))
            {
                opiderror = "First three characters of Operator ID must be a valid country code";
                return false;
            }

            // remove the hyphen
            string id = txt_UserID.Text.Replace("-", "");
            // remove the first three characters
            id = id.Substring(3);
            // get the 13th character
            char chksum_to_test = id[12];
            // remove the 13th character
            id = id.Remove(12, 1);

            if (Validate(id) != chksum_to_test)
            {
                opiderror = "Operator ID checksum failed";
                return false;
            }

            return true;

        }




        private void checkUID()
        {

            _uas_id = (!String.IsNullOrEmpty(TXT_UAS_ID.Text) && CMB_uas_id_type.SelectedIndex > 0 && CMB_uas_type.SelectedIndex > 0);            

            // Note - this needs to be updated later to accomondate a Standard Remote ID Configuratoin
            if (_uas_id && CMB_uas_id_type.SelectedIndex > 0 && CMB_uas_type.SelectedIndex > 0)
            {
                myDID.UAS_ID = TXT_UAS_ID.Text;

                myDID.UA_type = (MAVLink.MAV_ODID_UA_TYPE) CMB_uas_type.SelectedIndex;
                myDID.UAS_ID_type = (MAVLink.MAV_ODID_ID_TYPE) CMB_uas_id_type.SelectedIndex;
            }

            myDID.category_eu = (MAVLink.MAV_ODID_CATEGORY_EU)CMB_EU_Category.SelectedIndex;
            myDID.class_eu = (MAVLink.MAV_ODID_CLASS_EU)CMB_EU_Class.SelectedIndex;


            if (TXT_self_id_TXT.Text.Length > 0)
            {
                // Send Self ID Info
                myDID.description = TXT_self_id_TXT.Text;
                myDID.description_type = (MAVLink.MAV_ODID_DESC_TYPE) CMB_self_id_type.SelectedIndex;
            }

            if (txt_UserID.Text.Length > 0 && checkOperatorID())
            {
                //remove the last four character from txt_UserID.Text and set myDID.operator_id to that
                myDID.operator_id = txt_UserID.Text.Substring(0, txt_UserID.Text.Length - 4);
                myDID.operator_id_type = (MAVLink.MAV_ODID_OPERATOR_ID_TYPE)CMB_op_id_type.SelectedIndex;
            }


            
        }
        public static char Validate(string input)
        {
            input = input.ToUpper().Replace("-", ""); // Convert to uppercase and remove hyphens
            int sum = 0;
            bool alternate = true;

            for (int i = 0; i < input.Length; i++)
            {
                int n = CharToValue(input[i]);

                if (alternate)
                {
                    n *= 2;
                    if (n > 35)
                    {
                        n = (n / 36) + (n % 36);
                    }
                }
                sum += n;
                alternate = !alternate;
            }

            return ValueToChar((36 - (sum % 36)));
        }

        private static int CharToValue(char c)
        {
            if (char.IsDigit(c))
            {
                return c - '0';
            }

            else if (char.IsLetter(c))
            {
                return c - 'A' + 10;
            }
            else
            {
                return 0;

            }
        }

        private static char ValueToChar(int value)
        {
            string encode = "0123456789abcdefghijklmnopqrstuvwxyz";
            if (value < 0 || value > 35) return '-';    //Error !
            return encode[value];
        }

        private void checkGCSGPS()
        {
            try 
            {
                // Check NMEA GPS information
                NMEA_GPS_Connection.PointNMEA _gps_data = nmea_GPS_1.getPointNMEA();

                

                // Sanity Check
                if (_gps_data.Lat != 0.0 && _gps_data.Lng != 0.0)
                {

                    gotolocation.Lat = _gps_data.Lat; 
                    gotolocation.Lng = _gps_data.Lng;
                    gotolocation.Alt = _gps_data.Alt; 

                    if (_host != null)
                        _host.comPort.MAV.cs.Base = gotolocation;

                    myDID.operator_latitude = _gps_data.Lat;
                    myDID.operator_longitude = _gps_data.Lng;
                    myDID.operator_altitude_geo = (float)_gps_data.Alt_WGS84;
                    myDID.operator_location_type = MAVLink.MAV_ODID_OPERATOR_LOCATION_TYPE.LIVE_GNSS;

                    myDID.since_last_msg_ms = nmea_GPS_1.last_gps_msg.ElapsedMilliseconds; 

                }
            } catch
            {
                Console.WriteLine("Error Setting NMEA GPS Data");
            }

            // Check GCS GPS
            if (nmea_GPS_1.last_gps_msg.ElapsedMilliseconds > 5000)
            {
                //TODO Fix
                _gcs_gps = false;
            }
            else if (gotolocation.Lat == 0.0 || gotolocation.Lng == 0.0)
            {
                LED_gps_valid.Color = Color.Orange;
                _gcs_gps = false;
            }
            else if (gpsHasSBAS == false)
            {
                LED_gps_valid.Color = Color.Yellow;
                _gcs_gps = true;  // NOTE: This may need to be changed in the future to enforce SBAS only solutions
            }
            else
            {
                LED_gps_valid.Color = Color.Green;
                _gcs_gps = true;
            }
        }

        private void LBL_version_DoubleClick(object sender, EventArgs e)
        {

        }

        private void addStatusMessage(String msg)
        {
            TXT_ODID_Status.Text = DateTime.Now.ToString("HH:mm:ss") + " - " + msg + Environment.NewLine + TXT_ODID_Status.Text; 

            // TODO: Add a cleanup step here. 
        }

        private void txt_UserID_TextChanged(object sender, EventArgs e)
        {
            Validate(txt_UserID.Text);
            if (checkOperatorID())
            {
                lOpIDStatus.Text = "Operator ID OK";
                lOpIDStatus.ForeColor = Color.Green;
            }
            else
            {
                lOpIDStatus.Text = opiderror;
                lOpIDStatus.ForeColor = Color.Red;
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            Settings.Instance["ODID_UAS_ID"] = TXT_UAS_ID.Text; 
        }

        private void mainloop()
        {
            threadrun = true;
            while (threadrun)
            {
                DateTime _now = DateTime.Now;
                try
                {
                    if (_now > _last_time_1.AddSeconds(1.0 / _update_rate_hz_1))
                    {
                        // Check GPS
                        if (hasODID) {
                            checkODIDMsgs();

                            checkUID();

                            checkODID_OK();
                        }
                        _last_time_1 = DateTime.Now;
                    }
                }
                catch
                {
                    System.Threading.Thread.Sleep((int)(1000 / _update_rate_hz_1));
                }

                try
                {
                    if (_now > _last_time_2.AddSeconds(1.0 / _update_rate_hz_2))
                    {

                        checkGCSGPS();

                        start_sub();
                        _last_time_2 = DateTime.Now;
                    }
                }
                catch
                {
                    System.Threading.Thread.Sleep((int)(1000 / _update_rate_hz_2));
                }


                System.Threading.Thread.Sleep((int)25);
            }
        }


        private List<KeyValuePair<string, string>> Countries = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair < string,string > ("---", "- none -"),
                new KeyValuePair < string,string > ("AFG", "Afghanistan"),
                new KeyValuePair < string,string > ("ALB", "Albania"),
                new KeyValuePair < string,string > ("DZA", "Algeria"),
                new KeyValuePair < string,string > ("ASM", "American Samoa"),
                new KeyValuePair < string,string > ("AND", "Andorra"),
                new KeyValuePair < string,string > ("AGO", "Angola"),
                new KeyValuePair < string,string > ("AIA", "Anguilla"),
                new KeyValuePair < string,string > ("ATA", "Antarctica"),
                new KeyValuePair < string,string > ("ATG", "Antigua and Barbuda"),
                new KeyValuePair < string,string > ("ARG", "Argentina"),
                new KeyValuePair < string,string > ("ARM", "Armenia"),
                new KeyValuePair < string,string > ("ABW", "Aruba"),
                new KeyValuePair < string,string > ("AUS", "Australia"),
                new KeyValuePair < string,string > ("AUT", "Austria"),
                new KeyValuePair < string,string > ("AZE", "Azerbaijan"),
                new KeyValuePair < string,string > ("BHS", "Bahamas (the)"),
                new KeyValuePair < string,string > ("BHR", "Bahrain"),
                new KeyValuePair < string,string > ("BGD", "Bangladesh"),
                new KeyValuePair < string,string > ("BRB", "Barbados"),
                new KeyValuePair < string,string > ("BLR", "Belarus"),
                new KeyValuePair < string,string > ("BEL", "Belgium"),
                new KeyValuePair < string,string > ("BLZ", "Belize"),
                new KeyValuePair < string,string > ("BEN", "Benin"),
                new KeyValuePair < string,string > ("BMU", "Bermuda"),
                new KeyValuePair < string,string > ("BTN", "Bhutan"),
                new KeyValuePair < string,string > ("BOL", "Bolivia"),
                new KeyValuePair < string,string > ("BES", "Bonaire"),
                new KeyValuePair < string,string > ("BIH", "Bosnia and Herzegovina"),
                new KeyValuePair < string,string > ("BWA", "Botswana"),
                new KeyValuePair < string,string > ("BVT", "Bouvet Island"),
                new KeyValuePair < string,string > ("BRA", "Brazil"),
                new KeyValuePair < string,string > ("IOT", "British Indian Ocean Territory"),
                new KeyValuePair < string,string > ("BRN", "Brunei Darussalam"),
                new KeyValuePair < string,string > ("BGR", "Bulgaria"),
                new KeyValuePair < string,string > ("BFA", "Burkina Faso"),
                new KeyValuePair < string,string > ("BDI", "Burundi"),
                new KeyValuePair < string,string > ("CPV", "Cabo Verde"),
                new KeyValuePair < string,string > ("KHM", "Cambodia"),
                new KeyValuePair < string,string > ("CMR", "Cameroon"),
                new KeyValuePair < string,string > ("CAN", "Canada"),
                new KeyValuePair < string,string > ("CYM", "Cayman Islands"),
                new KeyValuePair < string,string > ("CAF", "Central African Republic"),
                new KeyValuePair < string,string > ("TCD", "Chad"),
                new KeyValuePair < string,string > ("CHL", "Chile"),
                new KeyValuePair < string,string > ("CHN", "China"),
                new KeyValuePair < string,string > ("CXR", "Christmas Island"),
                new KeyValuePair < string,string > ("CCK", "Cocos Islands"),
                new KeyValuePair < string,string > ("COL", "Colombia"),
                new KeyValuePair < string,string > ("COM", "Comoros"),
                new KeyValuePair < string,string > ("COD", "Congo (Democratic Republic)"),
                new KeyValuePair < string,string > ("COG", "Congo"),
                new KeyValuePair < string,string > ("COK", "Cook Islands"),
                new KeyValuePair < string,string > ("CRI", "Costa Rica"),
                new KeyValuePair < string,string > ("HRV", "Croatia"),
                new KeyValuePair < string,string > ("CUB", "Cuba"),
                new KeyValuePair < string,string > ("CUW", "Curaçao"),
                new KeyValuePair < string,string > ("CYP", "Cyprus"),
                new KeyValuePair < string,string > ("CZE", "Czechia"),
                new KeyValuePair < string,string > ("CIV", "Côte d'Ivoire"),
                new KeyValuePair < string,string > ("DNK", "Denmark"),
                new KeyValuePair < string,string > ("DJI", "Djibouti"),
                new KeyValuePair < string,string > ("DMA", "Dominica"),
                new KeyValuePair < string,string > ("DOM", "Dominican Republic"),
                new KeyValuePair < string,string > ("ECU", "Ecuador"),
                new KeyValuePair < string,string > ("EGY", "Egypt"),
                new KeyValuePair < string,string > ("SLV", "El Salvador"),
                new KeyValuePair < string,string > ("GNQ", "Equatorial Guinea"),
                new KeyValuePair < string,string > ("ERI", "Eritrea"),
                new KeyValuePair < string,string > ("EST", "Estonia"),
                new KeyValuePair < string,string > ("SWZ", "Eswatini"),
                new KeyValuePair < string,string > ("ETH", "Ethiopia"),
                new KeyValuePair < string,string > ("FLK", "Falkland Islands"),
                new KeyValuePair < string,string > ("FRO", "Faroe Islands"),
                new KeyValuePair < string,string > ("FJI", "Fiji"),
                new KeyValuePair < string,string > ("FIN", "Finland"),
                new KeyValuePair < string,string > ("FRA", "France"),
                new KeyValuePair < string,string > ("GUF", "French Guiana"),
                new KeyValuePair < string,string > ("PYF", "French Polynesia"),
                new KeyValuePair < string,string > ("ATF", "French Southern Ter/ies"),
                new KeyValuePair < string,string > ("GAB", "Gabon"),
                new KeyValuePair < string,string > ("GMB", "Gambia"),
                new KeyValuePair < string,string > ("GEO", "Georgia"),
                new KeyValuePair < string,string > ("DEU", "Germany"),
                new KeyValuePair < string,string > ("GHA", "Ghana"),
                new KeyValuePair < string,string > ("GIB", "Gibraltar"),
                new KeyValuePair < string,string > ("GRC", "Greece"),
                new KeyValuePair < string,string > ("GRL", "Greenland"),
                new KeyValuePair < string,string > ("GRD", "Grenada"),
                new KeyValuePair < string,string > ("GLP", "Guadeloupe"),
                new KeyValuePair < string,string > ("GUM", "Guam"),
                new KeyValuePair < string,string > ("GTM", "Guatemala"),
                new KeyValuePair < string,string > ("GGY", "Guernsey"),
                new KeyValuePair < string,string > ("GIN", "Guinea"),
                new KeyValuePair < string,string > ("GNB", "Guinea-Bissau"),
                new KeyValuePair < string,string > ("GUY", "Guyana"),
                new KeyValuePair < string,string > ("HTI", "Haiti"),
                new KeyValuePair < string,string > ("HMD", "Heard Island and McDonald Islands"),
                new KeyValuePair < string,string > ("VAT", "Holy See"),
                new KeyValuePair < string,string > ("HND", "Honduras"),
                new KeyValuePair < string,string > ("HKG", "Hong Kong"),
                new KeyValuePair < string,string > ("HUN", "Hungary"),
                new KeyValuePair < string,string > ("ISL", "Iceland"),
                new KeyValuePair < string,string > ("IND", "India"),
                new KeyValuePair < string,string > ("IDN", "Indonesia"),
                new KeyValuePair < string,string > ("IRN", "Iran"),
                new KeyValuePair < string,string > ("IRQ", "Iraq"),
                new KeyValuePair < string,string > ("IRL", "Ireland"),
                new KeyValuePair < string,string > ("IMN", "Isle of Man"),
                new KeyValuePair < string,string > ("ISR", "Israel"),
                new KeyValuePair < string,string > ("ITA", "Italy"),
                new KeyValuePair < string,string > ("JAM", "Jamaica"),
                new KeyValuePair < string,string > ("JPN", "Japan"),
                new KeyValuePair < string,string > ("JEY", "Jersey"),
                new KeyValuePair < string,string > ("JOR", "Jordan"),
                new KeyValuePair < string,string > ("KAZ", "Kazakhstan"),
                new KeyValuePair < string,string > ("KEN", "Kenya"),
                new KeyValuePair < string,string > ("KIR", "Kiribati"),
                new KeyValuePair < string,string > ("PRK", "Korea North"),
                new KeyValuePair < string,string > ("KOR", "Korea South"),
                new KeyValuePair < string,string > ("KWT", "Kuwait"),
                new KeyValuePair < string,string > ("KGZ", "Kyrgyzstan"),
                new KeyValuePair < string,string > ("LAO", "Lao"),
                new KeyValuePair < string,string > ("LVA", "Latvia"),
                new KeyValuePair < string,string > ("LBN", "Lebanon"),
                new KeyValuePair < string,string > ("LSO", "Lesotho"),
                new KeyValuePair < string,string > ("LBR", "Liberia"),
                new KeyValuePair < string,string > ("LBY", "Libya"),
                new KeyValuePair < string,string > ("LIE", "Liechtenstein"),
                new KeyValuePair < string,string > ("LTU", "Lithuania"),
                new KeyValuePair < string,string > ("LUX", "Luxembourg"),
                new KeyValuePair < string,string > ("MAC", "Macao"),
                new KeyValuePair < string,string > ("MDG", "Madagascar"),
                new KeyValuePair < string,string > ("MWI", "Malawi"),
                new KeyValuePair < string,string > ("MYS", "Malaysia"),
                new KeyValuePair < string,string > ("MDV", "Maldives"),
                new KeyValuePair < string,string > ("MLI", "Mali"),
                new KeyValuePair < string,string > ("MLT", "Malta"),
                new KeyValuePair < string,string > ("MHL", "Marshall Islands"),
                new KeyValuePair < string,string > ("MTQ", "Martinique"),
                new KeyValuePair < string,string > ("MRT", "Mauritania"),
                new KeyValuePair < string,string > ("MUS", "Mauritius"),
                new KeyValuePair < string,string > ("MYT", "Mayotte"),
                new KeyValuePair < string,string > ("MEX", "Mexico"),
                new KeyValuePair < string,string > ("FSM", "Micronesia"),
                new KeyValuePair < string,string > ("MDA", "Moldova"),
                new KeyValuePair < string,string > ("MCO", "Monaco"),
                new KeyValuePair < string,string > ("MNG", "Mongolia"),
                new KeyValuePair < string,string > ("MNE", "Montenegro"),
                new KeyValuePair < string,string > ("MSR", "Montserrat"),
                new KeyValuePair < string,string > ("MAR", "Morocco"),
                new KeyValuePair < string,string > ("MOZ", "Mozambique"),
                new KeyValuePair < string,string > ("MMR", "Myanmar"),
                new KeyValuePair < string,string > ("NAM", "Namibia"),
                new KeyValuePair < string,string > ("NRU", "Nauru"),
                new KeyValuePair < string,string > ("NPL", "Nepal"),
                new KeyValuePair < string,string > ("NLD", "Netherlands"),
                new KeyValuePair < string,string > ("NCL", "New Caledonia"),
                new KeyValuePair < string,string > ("NZL", "New Zealand"),
                new KeyValuePair < string,string > ("NIC", "Nicaragua"),
                new KeyValuePair < string,string > ("NER", "Niger"),
                new KeyValuePair < string,string > ("NGA", "Nigeria"),
                new KeyValuePair < string,string > ("NIU", "Niue"),
                new KeyValuePair < string,string > ("NFK", "Norfolk Island"),
                new KeyValuePair < string,string > ("MKD", "North Macedonia"),
                new KeyValuePair < string,string > ("MNP", "Northern Mariana Islands"),
                new KeyValuePair < string,string > ("NOR", "Norway"),
                new KeyValuePair < string,string > ("OMN", "Oman"),
                new KeyValuePair < string,string > ("PAK", "Pakistan"),
                new KeyValuePair < string,string > ("PLW", "Palau"),
                new KeyValuePair < string,string > ("PSE", "Palestine, State of"),
                new KeyValuePair < string,string > ("PAN", "Panama"),
                new KeyValuePair < string,string > ("PNG", "Papua New Guinea"),
                new KeyValuePair < string,string > ("PRY", "Paraguay"),
                new KeyValuePair < string,string > ("PER", "Peru"),
                new KeyValuePair < string,string > ("PHL", "Philippines (the)"),
                new KeyValuePair < string,string > ("PCN", "Pitcairn"),
                new KeyValuePair < string,string > ("POL", "Poland"),
                new KeyValuePair < string,string > ("PRT", "Portugal"),
                new KeyValuePair < string,string > ("PRI", "Puerto Rico"),
                new KeyValuePair < string,string > ("QAT", "Qatar"),
                new KeyValuePair < string,string > ("ROU", "Romania"),
                new KeyValuePair < string,string > ("RUS", "Russian Federation"),
                new KeyValuePair < string,string > ("RWA", "Rwanda"),
                new KeyValuePair < string,string > ("REU", "Réunion"),
                new KeyValuePair < string,string > ("BLM", "Saint Barthélemy"),
                new KeyValuePair < string,string > ("SHN", "Saint Helena"),
                new KeyValuePair < string,string > ("KNA", "Saint Kitts and Nevis"),
                new KeyValuePair < string,string > ("LCA", "Saint Lucia"),
                new KeyValuePair < string,string > ("MAF", "Saint Martin"),
                new KeyValuePair < string,string > ("SPM", "Saint Pierre - Miquelon"),
                new KeyValuePair < string,string > ("VCT", "Saint Vincent - Grenadines"),
                new KeyValuePair < string,string > ("WSM", "Samoa"),
                new KeyValuePair < string,string > ("SMR", "San Marino"),
                new KeyValuePair < string,string > ("STP", "Sao Tome and Principe"),
                new KeyValuePair < string,string > ("SAU", "Saudi Arabia"),
                new KeyValuePair < string,string > ("SEN", "Senegal"),
                new KeyValuePair < string,string > ("SRB", "Serbia"),
                new KeyValuePair < string,string > ("SYC", "Seychelles"),
                new KeyValuePair < string,string > ("SLE", "Sierra Leone"),
                new KeyValuePair < string,string > ("SGP", "Singapore"),
                new KeyValuePair < string,string > ("SXM", "Sint Maarten"),
                new KeyValuePair < string,string > ("SVK", "Slovakia"),
                new KeyValuePair < string,string > ("SVN", "Slovenia"),
                new KeyValuePair < string,string > ("SLB", "Solomon Islands"),
                new KeyValuePair < string,string > ("SOM", "Somalia"),
                new KeyValuePair < string,string > ("ZAF", "South Africa"),
                new KeyValuePair < string,string > ("SGS", "South Georgia & Sandwich Islands"),
                new KeyValuePair < string,string > ("SSD", "South Sudan"),
                new KeyValuePair < string,string > ("ESP", "Spain"),
                new KeyValuePair < string,string > ("LKA", "Sri Lanka"),
                new KeyValuePair < string,string > ("SDN", "Sudan (the)"),
                new KeyValuePair < string,string > ("SUR", "Suriname"),
                new KeyValuePair < string,string > ("SJM", "Svalbard and Jan Mayen"),
                new KeyValuePair < string,string > ("SWE", "Sweden"),
                new KeyValuePair < string,string > ("CHE", "Switzerland"),
                new KeyValuePair < string,string > ("SYR", "Syrian"),
                new KeyValuePair < string,string > ("TWN", "Taiwan"),
                new KeyValuePair < string,string > ("TJK", "Tajikistan"),
                new KeyValuePair < string,string > ("TZA", "Tanzania"),
                new KeyValuePair < string,string > ("THA", "Thailand"),
                new KeyValuePair < string,string > ("TLS", "Timor-Leste"),
                new KeyValuePair < string,string > ("TGO", "Togo"),
                new KeyValuePair < string,string > ("TKL", "Tokelau"),
                new KeyValuePair < string,string > ("TON", "Tonga"),
                new KeyValuePair < string,string > ("TTO", "Trinidad and Tobago"),
                new KeyValuePair < string,string > ("TUN", "Tunisia"),
                new KeyValuePair < string,string > ("TUR", "Turkey"),
                new KeyValuePair < string,string > ("TKM", "Turkmenistan"),
                new KeyValuePair < string,string > ("TCA", "Turks and Caicos Islands"),
                new KeyValuePair < string,string > ("TUV", "Tuvalu"),
                new KeyValuePair < string,string > ("UGA", "Uganda"),
                new KeyValuePair < string,string > ("UKR", "Ukraine"),
                new KeyValuePair < string,string > ("ARE", "United Arab Emirates"),
                new KeyValuePair < string,string > ("GBR", "United Kingdom and Northern Ireland"),
                new KeyValuePair < string,string > ("UMI", "United States Minor Outlying Islands"),
                new KeyValuePair < string,string > ("USA", "United States of America"),
                new KeyValuePair < string,string > ("URY", "Uruguay"),
                new KeyValuePair < string,string > ("UZB", "Uzbekistan"),
                new KeyValuePair < string,string > ("VUT", "Vanuatu"),
                new KeyValuePair < string,string > ("VEN", "Venezuela"),
                new KeyValuePair < string,string > ("VNM", "Viet Nam"),
                new KeyValuePair < string,string > ("VGB", "Virgin Islands (British)"),
                new KeyValuePair < string,string > ("VIR", "Virgin Islands (U.S.)"),
                new KeyValuePair < string,string > ("WLF", "Wallis and Futuna"),
                new KeyValuePair < string,string > ("ESH", "Western Sahara"),
                new KeyValuePair < string,string > ("YEM", "Yemen"),
                new KeyValuePair < string,string > ("ZMB", "Zambia"),
                new KeyValuePair < string,string > ("ZWE", "Zimbabwe"),
                new KeyValuePair < string,string > ("ALA", "Aland Islands")
            };

    }

}
