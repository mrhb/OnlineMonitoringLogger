using InfluxDB.Collector;
using OnlineMonitoringLog.Drivers.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SocketAsyncServer.Core
{
    class LoggToInfluxDb
    {
       static LoggToInfluxDb()
        {

        //*******InfluxDb Init*********************
        Metrics.Collector = new CollectorConfiguration()
                 .Tag.With("Company", "TetaPower")
                
                 .Batch.AtInterval(TimeSpan.FromSeconds(2))
                 .WriteTo.InfluxDB("http://localhost:8086", "telegraf")
                 // .WriteTo.InfluxDB("udp://localhost:8089", "data")
                 .CreateCollector();
        //***************
        }
        public static void Logg(byte[] data, int UnitId)
        {
            var Registers = ExtractHoldingRegister(data);

            var datas = new Dictionary<string, object>();
            // Console Output
            var randGen = new Random();
            for (int i = 0; i < Math.Min(Regs.Length, Registers.Length); i++)
            {
                int val = Registers[i];// + randGen.Next(500);
                var item = Regs[i];
                if (item != "")//item.RecievedData(val, DateTime.Now))
                {
                    datas.Add(item, val);
                }
            }
            //*******InfluxDb ********
            var tags = new Dictionary<string, string>() {
                   { "Company", "TetaPower" },
                { "UnitId",UnitId.ToString() },
            };
            Metrics.Write("ModbusLogger", datas, tags);

        }

        public static int[] ExtractHoldingRegister(byte[] data)
        {
            var quantity = Regs.Length;
           var  response = new int[quantity];
            for (int i = 0; i < quantity; i++)
            {
                byte lowByte;
                byte highByte;
                highByte = data[9 + i * 2];
                lowByte = data[9 + i * 2 + 1];

                data[9 + i * 2] = lowByte;
                data[9 + i * 2 + 1] = highByte;

                response[i] = BitConverter.ToInt16(data, (9 + i * 2));
            }
            return (response);

        }
       static string[] Regs = new string[]
       {
        "",                //00
        "",                //01
        "",                //02
        "",                //03
        "",                //04
        "",                //05
        "",                //06
        "",                //07
        "",                //08
        "",                //09
        "GAC_SP",          //10
        "GAC_FB",          //11
        "GAC_PWM",         //12
        "GAC_Current",     //13
        "RPM_SP",          //14
        "RPM_FB",          //15
        "RPM_Shifter",     //16
        "MAP_p",           //17
        "MAP_T",           //18
        "Thermocouple1",   //19
        "Thermocouple2",   //20
        "Oil_P",           //21
        "Water_T",         //22
        "Advance",         //23
        "IgnitionTime",    //24
        "",                //25
        "",                //26
        "",                //27
        "",                //28
        "",                //29
        "",                //30
        "Power",           //31
        "RunT_H",          //32
        "RunT_L",          //33
       };
    }
}
