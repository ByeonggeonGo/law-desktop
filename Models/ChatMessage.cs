using System;
using System.Collections.Generic;

namespace LawDesktop.Models
{
    public class ChatMessage
    {
        public string Sender { get; set; } = string.Empty; // "User" or "AI"
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsAi => Sender == "AI";
        public bool IsUser => Sender == "User";

        // WPF UI Binding Helper Properties
        public string Alignment => IsUser ? "Right" : "Left";
        public string BubbleBackground => IsUser ? "#0EA5E9" : "#1E293B"; // Sky 500 / Slate 800
        public string BubbleBorder => IsUser ? "#0284C7" : "#334155";
        public string TextColor => "#F8FAFC"; // Gray 50

        // Citation Guard Summary Result
        public string GuardSummary { get; set; } = string.Empty;
        public bool HasGuardSummary => !string.IsNullOrEmpty(GuardSummary);
        public bool IsHallucinated { get; set; }
        public bool IsPartialVerified { get; set; }

        // Extracted Citation links list
        public List<Citation> Citations { get; set; } = new();
        public bool HasCitations => Citations != null && Citations.Count > 0;
    }
}
