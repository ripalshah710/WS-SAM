using System;
using System.Collections.Generic;
using System.Text;

namespace WS_POC.Models
{
    public class Message
    {
        public enum MessageType
        {
            EventCreated,
            EventApproved,
            EventSchedule,
            MasterEventCreated,
            RoomSetup,
            ClockHour,
            RequestClockHour,
            SessionRegistration,
            NotifyFaci,
            ClockHourDenial,
            ClockHourResubmit,
            DistrictContact,
        }

        public string attendee_pk { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public bool IsBodyHtml { get; set; }
        public string ToAddress { get; set; }
        public string FromAddress { get; set; }
    }
}
