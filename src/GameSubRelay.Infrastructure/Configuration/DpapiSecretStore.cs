using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using GameSubRelay.Core.Configuration;

namespace GameSubRelay.Infrastructure.Configuration;

public sealed class DpapiSecretStore : ISecretStore
{
    public const string DefaultFileName = "secrets.json.dpapi";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly FileInfo _secretFile;

    public DpapiSecretStore()
        : this(new FileInfo(GetDefaultSecretFilePath()))
    {
    }

    public DpapiSecretStore(string directoryPath)
        : this(new DirectoryInfo(directoryPath))
    {
    }

    public DpapiSecretStore(string path, bool isFilePath)
        : this(new FileInfo(isFilePath ? path : Path.Combine(path, DefaultFileName)))
    {
    }

    public DpapiSecretStore(DirectoryInfo directory)
        : this(new FileInfo(Path.Combine(directory.FullName, DefaultFileName)))
    {
    }

    public DpapiSecretStore(FileInfo secretFile)
    {
        _secretFile = secretFile;
    }

    public async ValueTask<SecretSettings> LoadSecretsAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_secretFile.FullName))
        {
            return SecretSettings.Empty;
        }

        var protectedBytes = await File.ReadAllBytesAsync(_secretFile.FullName, cancellationToken);
        var plaintext = DpapiCurrentUserProtector.Unprotect(protectedBytes);
        try
        {
            var envelope = JsonSerializer.Deserialize<SecretEnvelope>(plaintext, JsonOptions);
            return envelope?.ToSettings().Normalize() ?? SecretSettings.Empty;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    public async ValueTask SaveSecretsAsync(SecretSettings secrets, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(secrets);

        Directory.CreateDirectory(_secretFile.DirectoryName ?? ".");

        var envelope = SecretEnvelope.FromSettings(secrets.Normalize());
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);
        byte[] protectedBytes;
        try
        {
            protectedBytes = DpapiCurrentUserProtector.Protect(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }

        var tempFile = new FileInfo(_secretFile.FullName + ".tmp");
        await File.WriteAllBytesAsync(tempFile.FullName, protectedBytes, cancellationToken);
        File.Move(tempFile.FullName, _secretFile.FullName, overwrite: true);
    }

    private static string GetDefaultSecretFilePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "GameSubRelay", DefaultFileName);
    }

    private sealed record SecretEnvelope(VolcengineSecrets Volcengine)
    {
        public static SecretEnvelope FromSettings(SecretSettings settings) => new(new VolcengineSecrets(
            settings.VolcengineAccessKeyId,
            settings.VolcengineSecretAccessKey,
            settings.VolcengineAppKey,
            settings.VolcengineAstAccessKey,
            settings.VolcengineTtsAppId,
            settings.VolcengineTtsToken,
            settings.VolcengineTtsCluster,
            settings.VolcengineAsrAppKey,
            settings.VolcengineAsrAccessKey));

        public SecretSettings ToSettings() => new(
            Volcengine.AccessKeyId ?? string.Empty,
            Volcengine.SecretAccessKey ?? string.Empty,
            Volcengine.AppKey ?? string.Empty,
            Volcengine.AstAccessKey ?? string.Empty,
            Volcengine.TtsAppId ?? string.Empty,
            Volcengine.TtsToken ?? string.Empty,
            Volcengine.TtsCluster ?? string.Empty,
            Volcengine.AsrAppKey ?? string.Empty,
            Volcengine.AsrAccessKey ?? string.Empty);
    }

    private sealed record VolcengineSecrets(
        string AccessKeyId,
        string SecretAccessKey,
        string AppKey,
        string AstAccessKey,
        string TtsAppId,
        string TtsToken,
        string TtsCluster,
        string AsrAppKey = "",
        string AsrAccessKey = "");

    private static class DpapiCurrentUserProtector
    {
        private const int CryptProtectUiForbidden = 0x1;

        public static byte[] Protect(byte[] plaintext)
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("DPAPI secret persistence requires Windows.");
            }

            return ProtectCore(plaintext, protect: true);
        }

        public static byte[] Unprotect(byte[] protectedBytes)
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("DPAPI secret persistence requires Windows.");
            }

            return ProtectCore(protectedBytes, protect: false);
        }

        private static byte[] ProtectCore(byte[] input, bool protect)
        {
            var inputPointer = Marshal.AllocHGlobal(input.Length);
            try
            {
                Marshal.Copy(input, 0, inputPointer, input.Length);
                var inputBlob = new DataBlob(input.Length, inputPointer);
                DataBlob outputBlob;
                bool ok;

                if (protect)
                {
                    ok = CryptProtectData(
                        ref inputBlob,
                        "GameSubRelay secrets",
                        IntPtr.Zero,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        CryptProtectUiForbidden,
                        out outputBlob);
                }
                else
                {
                    ok = CryptUnprotectData(
                        ref inputBlob,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        CryptProtectUiForbidden,
                        out outputBlob);
                }

                if (!ok)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                try
                {
                    var output = new byte[outputBlob.Size];
                    Marshal.Copy(outputBlob.Data, output, 0, output.Length);
                    return output;
                }
                finally
                {
                    if (outputBlob.Data != IntPtr.Zero)
                    {
                        LocalFree(outputBlob.Data);
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(inputPointer);
            }
        }

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool CryptProtectData(
            ref DataBlob dataIn,
            string? dataDescription,
            IntPtr optionalEntropy,
            IntPtr reserved,
            IntPtr promptStruct,
            int flags,
            out DataBlob dataOut);

        [DllImport("crypt32.dll", SetLastError = true)]
        private static extern bool CryptUnprotectData(
            ref DataBlob dataIn,
            IntPtr dataDescription,
            IntPtr optionalEntropy,
            IntPtr reserved,
            IntPtr promptStruct,
            int flags,
            out DataBlob dataOut);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LocalFree(IntPtr handle);

        [StructLayout(LayoutKind.Sequential)]
        private struct DataBlob
        {
            public DataBlob(int size, IntPtr data)
            {
                Size = size;
                Data = data;
            }

            public int Size;
            public IntPtr Data;
        }
    }
}
