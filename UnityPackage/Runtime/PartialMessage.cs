using RiptideNetworking.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RiptideNetworking
{
    public static class PartialMessageHandler
    {
        public static uint id { get; set; }
        public static Client Client { get; set; }
        public static Server Server { get; set; }
        public static Dictionary<uint, PartialMessage> PartialMessages = new Dictionary<uint, PartialMessage>();
        /// <summary>This is for what partial messages you sent so the client can request packets they missed</summary>
        public static Dictionary<uint, PartialMessage> SentPartialMessages = new Dictionary<uint, PartialMessage>();

        public enum MessageDirection
        {
            SendToClient,
            SendToServer
        }

        public static void SendPartialMessage(ushort splitMessageInboundId, ushort splitMessageId, ushort id, byte[] bytes, ushort? toClientID, MessageDirection messageDirection)
        {
            var sentPartialMessage = new PartialMessage();
            if (toClientID != null)
                sentPartialMessage.FromClientID = toClientID.GetValueOrDefault();
            sentPartialMessage.PartialMessageID = PartialMessageHandler.id;

            Message splitMessageInbound = Message.Create(MessageSendMode.reliable, splitMessageInboundId);
            splitMessageInbound.AddUShort(id);
            splitMessageInbound.AddUInt(PartialMessageHandler.id);

            int unWrittenLength = 1237;
            int splitCount = bytes.Length / unWrittenLength; //Todo get Unwritten length from a message prop

            if (splitCount <= 0)
                splitCount = 1;

            if (bytes.Length <= unWrittenLength == false) //If it is only one partial message total it doesn't think there are 2
                if (bytes.Length % unWrittenLength != 0)
                    splitCount++;

            splitMessageInbound.AddInt(splitCount); //How many splits will be in this file

            if (messageDirection == MessageDirection.SendToServer)
                PartialMessageHandler.Client.Send(splitMessageInbound);
            else if (messageDirection == MessageDirection.SendToClient)
            {
                if (toClientID.HasValue)
                    PartialMessageHandler.Server.Send(splitMessageInbound, toClientID.Value);
                else
                    PartialMessageHandler.Server.SendToAll(splitMessageInbound);

                
            }

            uint splitMessageOrdinal = 0;
            foreach (byte[] copySlice in bytes.Slices(unWrittenLength))
            {
                splitMessageOrdinal++;

                Message sliceMessage = Message.Create(MessageSendMode.reliable, splitMessageId);
                sliceMessage.AddUInt(PartialMessageHandler.id);
                sliceMessage.AddUInt(splitMessageOrdinal);
                sliceMessage.AddBytes(copySlice);

                sentPartialMessage.AddPartialMessage(splitMessageOrdinal, copySlice, false);

                if (messageDirection == MessageDirection.SendToClient)
                    if (toClientID.HasValue)
                        PartialMessageHandler.Server.Send(sliceMessage, toClientID.Value);
                    else
                        PartialMessageHandler.Server.SendToAll(sliceMessage);
                else if (messageDirection == MessageDirection.SendToServer)
                    PartialMessageHandler.Client.Send(sliceMessage);
            }

            SentPartialMessages.Add(PartialMessageHandler.id, sentPartialMessage);

            PartialMessageHandler.id++;
        }

        public class PartialMessage
        {
            public ushort ServerToClientID { get; set; }
            public uint PartialMessageID { get; set; }
            public int PartialMessageCount { get; set; }
            public ushort FromClientID { get; set; }
            public Dictionary<uint, byte[]> MessageData { get; set; } = new Dictionary<uint, byte[]>();
            public bool IsDone => MessageData.Count >= PartialMessageCount;
            public List<uint> PartialMessageIDsRecieved { get; set; } = new List<uint>();
            public void AddPartialMessage(uint ordinal, byte[] bytes, bool doEventHandling = true)
            {
                PartialMessageIDsRecieved.Add(ordinal);

                this.MessageData.Add(ordinal, bytes);

                //if (this.MessageData.Any() == false)
                //    this.MessageData.Add(bytes);
                //else
                //    if (this.MessageData.Count < ordinal)
                //    this.MessageData.Add(bytes);
                //else if (this.MessageData.Count >= ordinal)
                //    this.MessageData.Insert((int)ordinal - 1, bytes);

                if (doEventHandling)
                {
                    PartialMessageProgress progressMessage = new PartialMessageProgress(this.PartialMessageID, this.PartialMessageCount, this.MessageData.Count, this.PartialMessageIDsRecieved);

                    if (PartialMessageHandler.Client != null)
                    {
                        if (PartialMessageHandler.Client.messageProgressHandlers.ContainsKey(this.ServerToClientID))
                            PartialMessageHandler.Client.messageProgressHandlers[this.ServerToClientID].Invoke(progressMessage);
                    }
                }

                //if (PartialMessageHandler.Server != null)
                //{
                //    if (PartialMessageHandler.Server.messageProgressHandlers.ContainsKey(this.ServerToClientID))
                //        PartialMessageHandler.Server.messageProgressHandlers[this.ServerToClientID].Invoke(progressMessage);
                //}

                if (this.IsDone && doEventHandling)
                {
                    Message message = new Message(this.MessageData.OrderBy(x => x.Key).SelectMany(byteArr => byteArr.Value).ToArray());

                    if (PartialMessageHandler.Client != null)
                        PartialMessageHandler.Client.messageHandlers[this.ServerToClientID].Invoke(message);

                    if (PartialMessageHandler.Server != null)
                        PartialMessageHandler.Server.messageHandlers[this.ServerToClientID].Invoke(FromClientID, message);
                }
            }
        }
    }
}
