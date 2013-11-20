using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IKSL
{
    class CodeSamples
    {
        private event EventHandler<object> AllFramesReady;

        public void Test()
        {
            AllFramesReady += CodeSamples_AllFramesReady;
        }

        private async void CodeSamples_AllFramesReady(object sender, object e)
        {
            //UI Thread
            Task<object> t1 = Task.Factory.StartNew<object>(() => DoTask1());
            Task<int> t2 = Task.Factory.StartNew<int>(() => DoTask2(1));
            Task<string> t3 = Task.Factory.StartNew<string>(() => DoTask3());

            await Task.WhenAll(t1, t2, t3);

            string r3 = t3.Result;
        }

        private object DoTask1() 
        { 
            //do something cpu intensive bla bla bla
            return null;
        }

        private int DoTask2(int argument)
        {
            return argument++;
        }

        private string DoTask3()
        {
            return string.Empty;
        }
    }
}
