import hashlib
import unittest

from verify_android_release import verify_certificate


class AndroidSigningCertificateTest(unittest.TestCase):
    def test_supported_signer_formats(self):
        certificate = b'test certificate'
        fingerprint = hashlib.sha256(certificate).hexdigest()
        for label in ('Signer #1 certificate', 'V3.0 Signer: certificate', 'V2 Signer: certificate'):
            with self.subTest(label=label):
                verify_certificate(label + ' SHA-256 digest: ' + fingerprint, certificate)
                verify_certificate(label + ' SHA-256 digest: ' + fingerprint.upper(), certificate)

    def test_wrong_certificate_rejected(self):
        with self.assertRaises(ValueError):
            verify_certificate('V3.0 Signer: certificate SHA-256 digest: ' + '0' * 64, b'original')

    def test_missing_certificate_rejected(self):
        with self.assertRaises(ValueError):
            verify_certificate('No signing certificate', b'original')

    def test_unexpected_additional_signer_rejected(self):
        fingerprint = hashlib.sha256(b'original').hexdigest()
        output = 'certificate SHA-256 digest: ' + fingerprint + '\ncertificate SHA-256 digest: ' + '0' * 64
        with self.assertRaises(ValueError):
            verify_certificate(output, b'original')
