using System;
using System.Collections.Generic;
using System.Text;

namespace RiptideNetworking
{
    public class PartialMessageProgress
    {
        public uint SplitMessageID { get; private set; }
        public int SplitMessageCount { get; private set; }
        public int SplitMessagesRecieved { get; private set; }
        public float PercentDone => (float)((float)SplitMessagesRecieved / SplitMessageCount) * 100;
        public List<uint> PartialMessageIDsRecieved { get; private set; } = new List<uint>();

        public PartialMessageProgress(uint splitMessageID, int splitMessageCount, int splitMessagesRecieved, List<uint> missingMessageIDs)
        {
            SplitMessageID = splitMessageID;
            SplitMessageCount = splitMessageCount;
            SplitMessagesRecieved = splitMessagesRecieved;
            PartialMessageIDsRecieved = missingMessageIDs;
        }
    }
}
