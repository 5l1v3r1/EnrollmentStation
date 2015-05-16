﻿using System;
using System.Xml.Serialization;

namespace EnrollmentStation.Code
{
    [Serializable]
    public class EnrolledYubikey
    {
        [XmlAttribute]
        public int DeviceSerial { get; set; }

        [XmlAttribute]
        public string Username { get; set; }

        [XmlAttribute]
        public string CA { get; set; }

        [XmlAttribute]
        public string CertificateThumbprint { get; set; }

        [XmlAttribute]
        public string CertificateSerial { get; set; }

        [XmlAttribute]
        public string ManagementKey { get; set; }

        [XmlAttribute]
        public string Chuid { get; set; }

        [XmlAttribute]
        public string PukKey { get; set; }

        [XmlAttribute]
        public DateTime EnrolledAt { get; set; }
    }
}