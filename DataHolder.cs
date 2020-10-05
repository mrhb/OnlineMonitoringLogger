using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;


namespace SocketAsyncServer
{
    class DataHolder
    {
        //Remember, if a socket uses a byte array for its buffer, that byte array is
        //unmanaged in .NET and can cause memory fragmentation. So, first write to the
        //buffer block used by the SAEA object. Then, you can copy that data to another
        //byte array, if you need to keep it or work on it, and want to be able to put
        //the SAEA object back in the pool quickly, or continue with the data 
        //transmission quickly.         
        //DataHolder has this byte array to which you can copy the data.
        internal Byte[] dataMessageReceived;

        internal Int32 receivedTransMissionId;

        internal Int32 sessionId;

        //for testing. With a packet analyzer this can help you see specific connections.
        internal EndPoint remoteEndpoint;


        private Int32 numberOfMessagesSent = 0;

        //We'll just send a string message. And have one or more messages, so
        //we need an array.
        internal string[] arrayOfMessagesToSend;


        //Since we are creating a List<T> of message data, we'll
        //need to decode it later, if we want to read a string.
        internal List<byte[]> listOfMessagesReceived = new List<byte[]>();




        public Int32 NumberOfMessagesSent
        {
            get
            {
                return this.numberOfMessagesSent;
            }
            set
            {
                this.numberOfMessagesSent = value;
            }
        }
        //write the array of messages to send
        internal void PutMessagesToSend(string[] theArrayOfMessagesToSend)
        {
            this.arrayOfMessagesToSend = theArrayOfMessagesToSend;
        }
    }
}
