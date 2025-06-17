using System;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class PersistorGeneratedFolderAttribute : Attribute
{
    public string FolderPath { get; }
    public PersistorGeneratedFolderAttribute(string folderPath)
    {
        FolderPath = folderPath;
    }
}
