using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using VoxPilot.Models;

namespace VoxPilot.Services;

public static class CredentialManager
{
    private const string OpenAiTarget = "VoxPilot.OpenAIApiKey";
    private const string OpenRouterTarget = "VoxPilot.OpenRouterApiKey";
    private const uint GenericCredential = 1;
    private const uint PersistLocalMachine = 2;

    public static void SaveApiKey(ApiProvider provider, string secret)
    {
        var target = GetTarget(provider);
        if (string.IsNullOrWhiteSpace(secret))
        {
            CredDelete(target, GenericCredential, 0);
            return;
        }

        var bytes = Encoding.Unicode.GetBytes(secret);
        var blob = Marshal.AllocCoTaskMem(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var credential = new NativeCredential
            {
                Type = GenericCredential,
                TargetName = target,
                CredentialBlobSize = (uint)bytes.Length,
                CredentialBlob = blob,
                Persist = PersistLocalMachine,
                UserName = Environment.UserName,
                AttributeCount = 0,
                Attributes = IntPtr.Zero,
                Comment = $"{GetDisplayName(provider)} API key for VoxPilot",
                TargetAlias = null
            };

            if (!CredWrite(ref credential, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not save the API key in Windows Credential Manager.");
        }
        finally
        {
            var zeros = new byte[bytes.Length];
            Marshal.Copy(zeros, 0, blob, zeros.Length);
            CryptographicOperations.ZeroMemory(bytes);
            Marshal.FreeCoTaskMem(blob);
        }
    }

    public static string? ReadApiKey(ApiProvider provider)
    {
        if (!CredRead(GetTarget(provider), GenericCredential, 0, out var pointer)) return null;
        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(pointer);
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0) return null;
            return Marshal.PtrToStringUni(credential.CredentialBlob, (int)credential.CredentialBlobSize / 2);
        }
        finally
        {
            CredFree(pointer);
        }
    }

    private static string GetTarget(ApiProvider provider) =>
        provider == ApiProvider.OpenAI ? OpenAiTarget : OpenRouterTarget;

    private static string GetDisplayName(ApiProvider provider) =>
        provider == ApiProvider.OpenAI ? "OpenAI" : "OpenRouter";

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite([In] ref NativeCredential credential, [In] uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, uint type, uint reservedFlag, out IntPtr credentialPointer);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree(IntPtr credential);
}
