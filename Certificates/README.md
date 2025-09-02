# UAS Device Certificates

This directory contains SSL certificates for secure communication with UAS-WAND devices using mbed TLS.

## Certificate Types Supported

### Client Certificates (.pfx, .p12)
- **Purpose**: Authentication with UAS devices
- **Format**: PKCS#12 format with private key
- **Password**: The system will try common passwords automatically:
  - No password (empty)
  - `uas`
  - `uaswand` 
  - `inductosense`
  - `password`
  - `admin`

### Server/CA Certificates (.crt, .cer)
- **Purpose**: Trust validation for UAS device certificates
- **Format**: X.509 certificate format
- **Usage**: Added to trusted certificate collection

## Adding Your UAS Certificates

1. **Copy certificate files** to this directory:
   ```
   Certificates/
   ├── uas-device-client.pfx    (Client certificate with private key)
   ├── uas-ca-root.crt          (Root CA certificate)
   └── uas-server.cer           (Server certificate)
   ```

2. **File naming**: Any filename is acceptable, only the extension matters:
   - `.pfx` or `.p12` for client certificates
   - `.crt` or `.cer` for server/CA certificates

3. **Password protection**: If your .pfx/.p12 files use a password not in the common list above, you may need to:
   - Rename the file to include a hint: `uas-cert-password123.pfx`
   - Or contact support to add the password to the common list

## SSL Configuration

The HTTPS transport is configured to work with mbed TLS characteristics:

- **Self-signed certificates**: Accepted (common for UAS devices)
- **Certificate name mismatches**: Accepted (when connecting by IP address)
- **Custom CA certificates**: Supported via .crt/.cer files
- **Client certificate authentication**: Supported via .pfx/.p12 files

## Security Notes

- Certificates in this directory are included in the application build
- Do not commit sensitive certificate files to version control
- Use `.gitignore` to exclude certificate files if needed
- Certificate validation is permissive for UAS device compatibility

## Troubleshooting

### Certificate Loading Issues
- Check file permissions (read access required)
- Verify certificate password if protected
- Ensure certificate format is correct (.pfx/.p12 for client, .crt/.cer for server)

### SSL Connection Issues  
- Verify UAS device is configured for SSL on port 443
- Check that mbed TLS is properly configured on the UAS device
- Review application logs for detailed SSL validation information

### Certificate Validation Logs
Enable debug logging to see certificate validation details:
```json
{
  "Logging": {
    "LogLevel": {
      "Inductobot.Services.Communication.UasWandHttpsTransport": "Debug"
    }
  }
}
```

## Installed UAS Certificates

The following certificates have been configured for your UAS-WAND devices:

### **Client Certificate (Mutual TLS Authentication)**
- **File**: `uas-client.pfx`
- **Password**: `uas`
- **Purpose**: Client authentication with UAS devices
- **Contains**: Private key + certificate for ESP32 HTTPS server example
- **Subject**: `ESP32 HTTPS server example`

### **Server Certificate (Trust Validation)**
- **File**: `uas-server.crt` 
- **Purpose**: Trust validation for UAS device server certificates
- **Subject**: `ESP32 HTTPS server example`
- **Valid**: 2018-10-17 to 2028-10-14 (self-signed)

### **Private Key (Development)**
- **File**: `uas-client.key`
- **Purpose**: Separate private key file (for reference)
- **Note**: Private key is also embedded in the .pfx file

## Certificate Details

These certificates are from an ESP32 HTTPS server example, which is compatible with mbed TLS implementations commonly used in UAS devices. The certificates provide:

- **Self-signed certificate support** (typical for embedded devices)
- **RSA 2048-bit encryption** (standard for industrial IoT)
- **10-year validity** (suitable for long-term deployments)

## Usage

The application will automatically:
1. Load `uas-client.pfx` with password `uas` for client authentication
2. Load `uas-server.crt` for server certificate trust validation  
3. Configure SSL validation appropriate for ESP32/mbed TLS devices
4. Accept self-signed certificates and IP-based connections

No additional configuration is required - the certificates are ready for use with UAS-WAND devices.