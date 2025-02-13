using System.Drawing;

namespace WindowsFormsApp7
{
    public class ConnectionInfo
    {
        public string Key { get; set; }
        public string Proto { get; set; }
        public string LocalEnd { get; set; }
        public string RemoteEnd { get; set; }
        public string State { get; set; }
        public int PID { get; set; }
        public string ProcessName { get; set; }
        public string ProcessPath { get; set; }
        public string Location { get; set; }
        public Color BackColor { get; set; }
    }
}
