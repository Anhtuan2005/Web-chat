using System;
using System.Collections.Generic;
using System.Linq;

namespace Online_chat.Models
{
    public class MessageStatisticsViewModel
    {
        public int TotalGroupMessages { get; set; }
        public int TotalPrivateMessages { get; set; }
        public int TotalMessages { get; set; }
        public int TotalUsers { get; set; }
        public int TotalGroups { get; set; }

        public List<TopSenderViewModel> TopGroupSenders { get; set; }
        public List<TopSenderViewModel> TopPrivateSenders { get; set; }
        public List<TopGroupViewModel> TopGroups { get; set; }
        public List<DailyMessageCountViewModel> GroupMessagesLast7Days { get; set; }
        public List<DailyMessageCountViewModel> PrivateMessagesLast7Days { get; set; }

        // Properties helper cho View
        public List<string> GroupMessagesLabels
        {
            get
            {
                if (GroupMessagesLast7Days == null) return new List<string>();
                return GroupMessagesLast7Days.Select(d => d.Date?.ToString("dd/MM") ?? "").ToList();
            }
        }

        public List<int> GroupMessagesData
        {
            get
            {
                if (GroupMessagesLast7Days == null) return new List<int>();
                return GroupMessagesLast7Days.Select(d => d.Count).ToList();
            }
        }

        public List<string> PrivateMessagesLabels
        {
            get
            {
                if (PrivateMessagesLast7Days == null) return new List<string>();
                return PrivateMessagesLast7Days.Select(d => d.Date?.ToString("dd/MM") ?? "").ToList();
            }
        }

        public List<int> PrivateMessagesData
        {
            get
            {
                if (PrivateMessagesLast7Days == null) return new List<int>();
                return PrivateMessagesLast7Days.Select(d => d.Count).ToList();
            }
        }
    }

    public class TopSenderViewModel
    {
        public string SenderId { get; set; }
        public string SenderUsername { get; set; }
        public int MessageCount { get; set; }
    }

    public class TopGroupViewModel
    {
        public int GroupId { get; set; }
        public string GroupName { get; set; }
        public int MessageCount { get; set; }
    }

    public class DailyMessageCountViewModel
    {
        public DateTime? Date { get; set; }
        public int Count { get; set; }
    }
}