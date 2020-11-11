using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;


namespace SocketAsyncServer
{
    class Mediator
    {
        
        private IncomingDataPreparer theIncomingDataPreparer;
        private OutgoingDataPreparer theOutgoingDataPreparer;
        private DataHolder theDataHolder;
        private SocketAsyncEventArgs saeaObject;
        private DataHoldingUserToken receiveSendToken;
       
        public Mediator(SocketAsyncEventArgs e)
        {
            
            this.saeaObject = e;
            this.theIncomingDataPreparer = new IncomingDataPreparer(saeaObject);
            this.theOutgoingDataPreparer = new OutgoingDataPreparer();            
        }

        internal IPEndPoint GetEndPoint()
        {
            var endpoint=(IPEndPoint)saeaObject.AcceptSocket.RemoteEndPoint;
            return endpoint;
        }

        internal IPAddress GetRemoteIp()
        {
            return ((IPEndPoint)saeaObject.AcceptSocket.RemoteEndPoint).Address;
        }
        internal int GetLocalPort()
        {
            return ((IPEndPoint)saeaObject.AcceptSocket.LocalEndPoint).Port;
        }


        internal void HandleData(DataHolder incomingDataHolder)
        {   
            if (Program.watchProgramFlow == true)   //for testing
            {
                receiveSendToken = (DataHoldingUserToken)this.saeaObject.UserToken;
                Program.testWriter.WriteLine("Mediator HandleData() " + receiveSendToken.TokenId);
            }
            theDataHolder = theIncomingDataPreparer.HandleReceivedData(incomingDataHolder, this.saeaObject);
        }

        internal void PrepareOutgoingData()
        {
            if (Program.watchProgramFlow == true)   //for testing
            {
                receiveSendToken = (DataHoldingUserToken)this.saeaObject.UserToken;
                Program.testWriter.WriteLine("Mediator PrepareOutgoingData() " + receiveSendToken.TokenId);
            }

            theOutgoingDataPreparer.PrepareOutgoingData(saeaObject, theDataHolder);            
        }

        
        internal SocketAsyncEventArgs GiveBack()
        {
            if (Program.watchProgramFlow == true)   //for testing
            {
                receiveSendToken = (DataHoldingUserToken)this.saeaObject.UserToken;
                Program.testWriter.WriteLine("Mediator GiveBack() " + receiveSendToken.TokenId);
            }
            return saeaObject;
        }
    }
}
