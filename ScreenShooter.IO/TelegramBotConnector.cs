using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using ScreenShooter.Actuator;

namespace ScreenShooter.IO
{
    class TelegramBotConnector: IConnector
    {
        public event EventHandler NewRequest;
        public async Task CreateSession()
        {
            throw new NotImplementedException();
        }

        public async Task EventLoop()
        {
            throw new NotImplementedException();
        }

        public async Task SendResult(ExecutionResult result)
        {
            throw new NotImplementedException();
        }

        public async Task DestroySession()
        {
            throw new NotImplementedException();
        }
    }
}
