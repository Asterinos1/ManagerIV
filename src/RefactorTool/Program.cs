using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RefactorTool;

class Program
{
    static void Main(string[] args)
    {
        var mainVmPath = @"C:\Users\PC\Documents\GitHub\Side-Hustle\src\ManagerIV\ViewModels\MainViewModel.cs";
        var libVmPath = @"C:\Users\PC\Documents\GitHub\Side-Hustle\src\ManagerIV\ViewModels\LibraryViewModel.cs";

        // 1. Rewrite LibraryViewModel
        var libTree = CSharpSyntaxTree.ParseText(File.ReadAllText(libVmPath));
        var libRoot = libTree.GetRoot();
        var libClass = libRoot.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault(c => c.Identifier.Text == "MainViewModel");
        
        var libMembersToKeep = new HashSet<string>
        {
            "_libraryDir", "_libraryManifestFile", "_libraryMods", "_selectedLibraryMod", "_selectedPluginMod", "_selectedScriptMod",
            "LibraryMods", "SelectedLibraryMod", "SelectedPluginMod", "SelectedScriptMod",
            "MainModsCollection", "PluginsCollection", "ScriptsCollection",
            "ClearLibraryCommand", "OpenLibraryDirCommand", "ImportModArchiveCommand", "ReorderModCommand", "ReorderUpCommand", "ReorderDownCommand", "DeleteModCommand", "ToggleModEnabledCommand", "SaveModDetailsCommand",
            "LoadLibrary", "SaveLibrary", "ClearLibraryAsync", "ClearLibraryInternalAsync", "OpenLibraryDirInExplorer",
            "ImportModArchiveAsync", "ApplyDerivedLibraryTags", "DeleteMod", "ReorderMod", "ReorderUp", "ReorderDown", "ToggleModEnabled", "SaveModDetails", "ApplyFilter"
        };

        var newLibMembers = new List<MemberDeclarationSyntax>();
        foreach (var member in libClass.Members)
        {
            if (member is FieldDeclarationSyntax field)
            {
                var name = field.Declaration.Variables.First().Identifier.Text;
                if (libMembersToKeep.Contains(name)) newLibMembers.Add(member);
            }
            else if (member is PropertyDeclarationSyntax prop)
            {
                var name = prop.Identifier.Text;
                if (libMembersToKeep.Contains(name)) newLibMembers.Add(member);
            }
            else if (member is MethodDeclarationSyntax method)
            {
                var name = method.Identifier.Text;
                if (libMembersToKeep.Contains(name)) newLibMembers.Add(member);
            }
            // Keep commands that are properties
        }

        // We also need the constructor for LibraryViewModel but we can inject that manually later.
        var newLibClass = libClass.WithIdentifier(SyntaxFactory.Identifier("LibraryViewModel")).WithMembers(SyntaxFactory.List(newLibMembers));
        var newLibRoot = libRoot.ReplaceNode(libClass, newLibClass);
        File.WriteAllText(libVmPath, newLibRoot.ToFullString());
        Console.WriteLine("LibraryViewModel pruned.");

        // 2. Rewrite MainViewModel
        var mainTree = CSharpSyntaxTree.ParseText(File.ReadAllText(mainVmPath));
        var mainRoot = mainTree.GetRoot();
        var mainClass = mainRoot.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault(c => c.Identifier.Text == "MainViewModel");
        
        var mainMembersToRemove = libMembersToKeep;
        
        var newMainMembers = new List<MemberDeclarationSyntax>();
        foreach (var member in mainClass.Members)
        {
            bool remove = false;
            if (member is FieldDeclarationSyntax field)
            {
                var name = field.Declaration.Variables.First().Identifier.Text;
                if (mainMembersToRemove.Contains(name)) remove = true;
            }
            else if (member is PropertyDeclarationSyntax prop)
            {
                var name = prop.Identifier.Text;
                if (mainMembersToRemove.Contains(name)) remove = true;
            }
            else if (member is MethodDeclarationSyntax method)
            {
                var name = method.Identifier.Text;
                if (mainMembersToRemove.Contains(name)) remove = true;
            }
            
            if (!remove)
            {
                newMainMembers.Add(member);
            }
        }

        var newMainClass = mainClass.WithMembers(SyntaxFactory.List(newMainMembers));
        var newMainRoot = mainRoot.ReplaceNode(mainClass, newMainClass);
        File.WriteAllText(mainVmPath, newMainRoot.ToFullString());
        Console.WriteLine("MainViewModel pruned.");
    }
}
