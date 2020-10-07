using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OnlineMonitoringLog.Drivers.Types
{
    public struct Metric
    {
        public Metric(string n, int i)
        {
            name = n;
            register = i;
        }
       public string name;
        public int register;


    }


    public class Regs
    {
   

    }
  
}
