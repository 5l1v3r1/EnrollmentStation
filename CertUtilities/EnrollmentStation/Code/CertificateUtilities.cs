using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using CERTCLIENTLib;
using CERTENROLLLib;

namespace EnrollmentStation.Code
{
    public enum RevokeReason
    {
        CRL_REASON_UNSPECIFIED = 0,
        CRL_REASON_KEY_COMPROMISE = 1,
        CRL_REASON_CA_COMPROMISE = 2,
        CRL_REASON_AFFILIATION_CHANGED = 3,
        CRL_REASON_SUPERSEDED = 4,
        CRL_REASON_CESSATION_OF_OPERATION = 5,
        CRL_REASON_CERTIFICATE_HOLD = 6
    }

    public static class CertificateUtilities
    {
        private const int CC_UIPICKCONFIG = 0x1;
        private const int CR_IN_BASE64 = 0x1;
        private const int CR_IN_FORMATANY = 0;
        private const int CR_DISP_ISSUED = 0x3;
        private const int CR_DISP_UNDER_SUBMISSION = 0x5;
        private const int CR_OUT_BASE64 = 0x1;

        public static bool Enroll(string username, string agentCertificate, string caConfig, string template, string csr, out X509Certificate2 cert)
        {
            cert = null;

            string argsKey = agentCertificate;
            string argsUser = username;

            X509Store store = new X509Store("My", StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);

            // Create a PKCS 10 inner request.
            CX509CertificateRequestPkcs10 pkcs10Req = new CX509CertificateRequestPkcs10();
            pkcs10Req.InitializeDecode(csr, EncodingType.XCN_CRYPT_STRING_BASE64_ANY);

            // Create a CMC outer request and initialize
            CX509CertificateRequestCmc cmcReq = new CX509CertificateRequestCmc();
            cmcReq.InitializeFromInnerRequestTemplateName(pkcs10Req, template);
            cmcReq.RequesterName = argsUser;

            CSignerCertificate signer = new CSignerCertificate();
            signer.Initialize(false, X509PrivateKeyVerify.VerifyNone, (EncodingType)0xc, argsKey);
            cmcReq.SignerCertificate = signer;

            // encode the request
            cmcReq.Encode();

            string strRequest = cmcReq.RawData[EncodingType.XCN_CRYPT_STRING_BASE64];

            CCertRequest objCertRequest = new CCertRequest();

            // Get CA config from UI
            string strCAConfig = caConfig;

            // Submit the request
            int iDisposition = objCertRequest.Submit(CR_IN_BASE64 | CR_IN_FORMATANY, strRequest, null, strCAConfig);

            // Check the submission status
            if (CR_DISP_ISSUED != iDisposition) // Not enrolled
            {
                string strDisposition = objCertRequest.GetDispositionMessage();

                if (CR_DISP_UNDER_SUBMISSION == iDisposition)
                {
                    Console.WriteLine("The submission is pending: " + strDisposition);
                    return false;
                }

                Console.WriteLine("The submission failed: " + strDisposition);
                Console.WriteLine("Last status: " + objCertRequest.GetLastStatus());
                return false;
            }

            // Get the certificate
            string strCert = objCertRequest.GetCertificate(CR_OUT_BASE64);
            byte[] rawCert = Convert.FromBase64String(strCert);

            cert = new X509Certificate2(rawCert);
            return true;
        }

        private static void Enroll(string publicKeyAsPem, string username, string agentCertificate, string caConfig)
        {
            string argsKey = agentCertificate;
            string argsUser = username;

            X509Store store = new X509Store("My", StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);

            publicKeyAsPem = string.Join("", publicKeyAsPem.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).Where(s => !s.StartsWith("--")));

            // Create a PKCS 10 inner request.
            CX509PublicKey pubKey = new CX509PublicKey();
            pubKey.InitializeFromEncodedPublicKeyInfo(publicKeyAsPem);

            CObjectId sha512 = new CObjectId();
            sha512.InitializeFromValue("2.16.840.1.101.3.4.2.3");

            CX509CertificateRequestPkcs10 pkcs10Req = new CX509CertificateRequestPkcs10();
            pkcs10Req.InitializeFromPublicKey(X509CertificateEnrollmentContext.ContextUser, pubKey, "");
            pkcs10Req.HashAlgorithm = sha512;

            string toSign = pkcs10Req.RawDataToBeSigned[EncodingType.XCN_CRYPT_STRING_HASHDATA];

            using (YubikeyPivTool piv = new YubikeyPivTool())
            {
                //piv.    
            }


            // Create a CMC outer request and initialize
            CX509CertificateRequestCmc cmcReq = new CX509CertificateRequestCmc();
            cmcReq.InitializeFromInnerRequestTemplateName(pkcs10Req, "SmartcardLogon");
            cmcReq.RequesterName = argsUser;

            CSignerCertificate signer = new CSignerCertificate();
            signer.Initialize(false, X509PrivateKeyVerify.VerifyNone, (EncodingType)0xc, argsKey);
            cmcReq.SignerCertificate = signer;

            // encode the request
            cmcReq.Encode();

            string strRequest = cmcReq.RawData[EncodingType.XCN_CRYPT_STRING_BASE64];

            CCertRequest objCertRequest = new CCertRequest();

            // Get CA config from UI
            string strCAConfig = caConfig;

            // Submit the request
            int iDisposition = objCertRequest.Submit(CR_IN_BASE64 | CR_IN_FORMATANY, strRequest, null, strCAConfig);

            // Check the submission status
            if (CR_DISP_ISSUED != iDisposition) // Not enrolled
            {
                string strDisposition = objCertRequest.GetDispositionMessage();

                if (CR_DISP_UNDER_SUBMISSION == iDisposition)
                {
                    Console.WriteLine("The submission is pending: " + strDisposition);
                    return;
                }

                Console.WriteLine("The submission failed: " + strDisposition);
                Console.WriteLine("Last status: " + objCertRequest.GetLastStatus());
                return;
            }

            // Get the certificate
            string strCert = objCertRequest.GetCertificate(CR_OUT_BASE64);

            string argsCrt = "tmp.crt";
            File.WriteAllText(argsCrt, "-----BEGIN CERTIFICATE-----\n" + strCert + "-----END CERTIFICATE-----\n");
        }

        public static void RevokeCertificate(string caConfig, string certificateSerialNumber, RevokeReason reason = RevokeReason.CRL_REASON_CESSATION_OF_OPERATION)
        {
            const string binary = @"Binaries\RevokeCert\RevokeCert.exe";

            string args = "\"" + caConfig + "\" " + ((int)reason) + " " + certificateSerialNumber;

            ProcessStartInfo start = new ProcessStartInfo(binary);
            start.Arguments = args;
            start.UseShellExecute = false;
            start.CreateNoWindow = true;

            start.RedirectStandardError = true;

            Process proc = Process.Start(start);
            proc.WaitForExit();

            string error = proc.StandardError.ReadToEnd();

            if (proc.ExitCode != 0)
                throw new Exception(error);
        }
    }
}