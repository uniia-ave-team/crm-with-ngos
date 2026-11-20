namespace Crm.Api.DemoMode.Sandbox;

/// <summary>
/// Locations of the files uploaded inside demo sandboxes. Each sandbox has its own folder
/// under the upload directory, so files never leak between visitors and disappear with the sandbox.
/// </summary>
public static class DemoSandboxPaths
{
    /// <summary>
    /// The folder inside the upload directory that holds all sandbox uploads.
    /// </summary>
    public const string SandboxesFolder = "_demo";

    /// <summary>
    /// Maps a storage folder (for example <c>users</c>) to the same folder inside a sandbox.
    /// </summary>
    /// <param name="sandboxId">The sandbox identifier.</param>
    /// <param name="folderName">The storage folder name.</param>
    /// <returns>The folder name relative to the upload directory.</returns>
    public static string GetSandboxFolder(string sandboxId, string folderName) =>
        Path.Combine(SandboxesFolder, sandboxId, folderName);

    /// <summary>
    /// Resolves the configured upload directory the same way the file storage does.
    /// </summary>
    /// <param name="uploadDirectory">The configured upload directory.</param>
    /// <returns>The absolute path of the upload directory.</returns>
    public static string ResolveUploadRoot(string uploadDirectory)
    {
        string root = Path.IsPathRooted(uploadDirectory)
            ? uploadDirectory
            : Path.Combine(AppContext.BaseDirectory, uploadDirectory);

        return Path.GetFullPath(root);
    }

    /// <summary>
    /// Deletes the files uploaded inside one sandbox.
    /// </summary>
    /// <param name="uploadRoot">The absolute path of the upload directory.</param>
    /// <param name="sandboxId">The sandbox identifier.</param>
    public static void DeleteSandboxUploads(string uploadRoot, string sandboxId) =>
        DeleteDirectory(Path.Combine(uploadRoot, SandboxesFolder, sandboxId));

    /// <summary>
    /// Deletes the files uploaded inside all sandboxes.
    /// </summary>
    /// <param name="uploadRoot">The absolute path of the upload directory.</param>
    public static void DeleteAllSandboxUploads(string uploadRoot) =>
        DeleteDirectory(Path.Combine(uploadRoot, SandboxesFolder));

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
