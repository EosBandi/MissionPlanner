using MissionPlanner.Utilities;
using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MissionPlanner
{
    public partial class webview2 : Form
    {
        public webview2()
        {
            InitializeComponent();
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            await InitializeAsync();

        }

        private async Task InitializeAsync()
        {
            await webView21.EnsureCoreWebView2Async(null);

            //These are the default values
            var accessToken = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJqdGkiOiJlMzhmZTVhOS1lMzA3LTQ5MmYtOTQ5OC02MDAxMTQyNjNjMDYiLCJpZCI6MjAxNDAwLCJpYXQiOjE3MTAyNzUyMDB9.PmmRRBJ8T5WLTf4DTGBKv85-wRK5fXToId6Vmfgonl8";
            var assetID = "3172264";
            var mapUrl = @"https://api.mapbox.com/styles/v1/soleongmbh/clnalh0zi03i401nz95kl874l/tiles/{z}/{x}/{y}?access_token=pk.eyJ1Ijoic29sZW9uZ21iaCIsImEiOiJjbG5jcDl1NnMwazJ2MnBuemZlazhvYnp5In0.fkj1qjzFo9jorLndFzijpw";

            //get 3d settings from the settings file
            if (Settings.Instance["render_accessToken"] != null)
            {
                accessToken = Settings.Instance["render_accessToken"];
            }
            if (Settings.Instance["render_assetID"] != null)
            {
                assetID = Settings.Instance["render_assetID"];
            }
            if (Settings.Instance["render_mapUrl"] != null)
            {
                mapUrl = Settings.Instance["render_mapUrl"];
            }

            //Write back the settings to the settings file
            Settings.Instance["render_accessToken"] = accessToken;
            Settings.Instance["render_assetID"] = assetID;
            Settings.Instance["render_mapUrl"] = mapUrl;



            //webView2.CoreWebView2.Navigate("https://www.microsoft.com");
            //Debug.WriteLine("after Navigate");
            string page = System.IO.File.ReadAllText(Settings.GetRunningDirectory() + "page.html");
            //find **accessToken** and **assetID** in the page.html and replace them with the actual values
            page = page.Replace("**accessToken**", accessToken);
            page = page.Replace("**assetID**", assetID);
            page = page.Replace("**mapUrl**", mapUrl);
            webView21.NavigateToString(page);

        }



    }
}
