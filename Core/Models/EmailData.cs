using System;
using System.Collections.Generic;
using NullGuard;

namespace SharpEML.Core.Models
{
    public class EmailData
    {
        public string FileName { get; set; }
        public long FileSize { get; set; }

        public string Subject { get; set; }
        public string Sender { get; set; }
        public bool HasAttachments { get; set; }
        public List<AttachmentInfo> Attachments { get; set; } = new List<AttachmentInfo>();
        public string Recipients { get; set; }
        public DateTime CreationTime { get; set; }
        public string FileHash { get; set; }

        // **确保默认值为空字符串，而不是 null**
        public string AttachmentNames { get; set; } = "";
        public string AttachmentSizes { get; set; } = "";
    }
}