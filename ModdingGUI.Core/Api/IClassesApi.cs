namespace ModdingGUI.Core.Api;

using System.Collections.Generic;
using ModdingGUI.Core.Classes;

/// <summary>
/// Read and edit <c>data/config/classdefs.tok</c> through raw, comment-preserving blocks.
/// </summary>
/// <remarks>
/// All members are synchronous, take the project lock, and back the file up into
/// <c>.mod-history/</c> before writing. Nothing is cached between calls: open → mutate →
/// write is the only contract, so an external edit is never clobbered by stale state.
/// </remarks>
public interface IClassesApi
{
    /// <summary>Class names in file order.</summary>
    IReadOnlyList<string> List(string projectPath);

    /// <summary>Every class block, in file order.</summary>
    IReadOnlyList<ClassDefDto> GetAll(string projectPath);

    /// <summary>One class block, or null when the class does not exist.</summary>
    ClassDefDto? Get(string projectPath, string className);

    /// <summary>
    /// Replace a key's value: the first matching line is rewritten in place and any
    /// further lines for the same key are dropped. Local edits not touching the value
    /// are preserved, and the file is left byte-identical when nothing changes.
    /// </summary>
    /// <param name="rawValue">Everything after the colon, e.g. <c>"Amazon"</c> or <c>2, 2, 0, 1, 0, 1</c>.</param>
    void SetField(string projectPath, string className, string key, string rawValue);

    /// <summary>Append a key line at the end of the class block (for repeatable keys).</summary>
    void AddField(string projectPath, string className, string key, string rawValue);

    /// <summary>Remove every line for a key from the class block.</summary>
    void RemoveField(string projectPath, string className, string key);

    /// <summary>Rename a class (<c>CREATECLASS:</c> header only; references are not rewritten).</summary>
    void Rename(string projectPath, string className, string newName);

    /// <summary>Add a new class block at the end of the file.</summary>
    void Add(string projectPath, string className, IReadOnlyList<ClassFieldDto> fields);

    /// <summary>Remove a class block, including the blank separator lines that follow it.</summary>
    void Remove(string projectPath, string className);

    /// <summary>
    /// Advisory checks for one class against the generated catalog. Read-only; nothing
    /// here blocks a write.
    /// </summary>
    IReadOnlyList<ClassValidationIssue> Validate(string projectPath, string className);
}
