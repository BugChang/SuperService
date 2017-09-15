using System.Collections.Generic;
using System.Net.NetworkInformation;

namespace SuperService
{
   public class MacHelper
    {
        public static IList<string> GetMacList()
        {
           var macList=new List<string>();
            NetworkInterface[] interfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface ni in interfaces)
            {
                if (ni.OperationalStatus == OperationalStatus.Up&&!string.IsNullOrEmpty(ni.GetPhysicalAddress().ToString()))
                {
                    macList.Add(ni.GetPhysicalAddress().ToString());
                }
            }
            return macList;

        }
    }
}
