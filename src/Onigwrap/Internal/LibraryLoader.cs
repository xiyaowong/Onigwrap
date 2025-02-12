/*
 * The following code is the modified version of mono/SkiaSharp LibraryLoader.cs
 * with Onigwrap specific modifications. The original code is distributed under
 * the following license:
 *
 * Copyright (c) 2015-2016 Xamarin, Inc.
 * Copyright (c) 2017-2018 Microsoft Corporation.
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy of
 * this software and associated documentation files (the "Software"), to deal in
 * the Software without restriction, including without limitation the rights to
 * use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
 * the Software, and to permit persons to whom the Software is furnished to do so,
 * subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
 * FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
 * COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
 * IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
 * CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 */

#if USE_LIBRARY_LOADER
using System;
using System.IO;
using System.Runtime.InteropServices;
#endif

namespace Onigwrap.Internal
{
#if USE_LIBRARY_LOADER
    // TODO: support non-Windows systems?
    internal static class LibraryLoader
    {
        static LibraryLoader()
        {
            Extension = ".dll";
        }

        public static string Extension { get; }

        public static IntPtr LoadLocalLibrary(string libraryName)
        {
            var libraryPath = GetLibraryPath(libraryName);

            var handle = LoadLibrary(libraryPath);
            if (handle == IntPtr.Zero)
                throw new DllNotFoundException($"Unable to load library '{libraryName}'.");

            return handle;

            static string GetLibraryPath(string libraryName)
            {
                var arch = RuntimeInformation.ProcessArchitecture switch
                {
                    Architecture.X64 => "x64",
                    Architecture.X86 => "x86",
                    Architecture.Arm => "arm",
                    Architecture.Arm64 => "arm64",
                    _ => ""
                };

                var libWithExt = libraryName;
                if (!libraryName.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
                    libWithExt += Extension;

                // 1. try alongside managed assembly
                var path = typeof(LibraryLoader).Assembly.Location;
                if (!string.IsNullOrEmpty(path))
                {
                    path = Path.GetDirectoryName(path);
                    if (CheckLibraryPath(path, arch, libWithExt, out var localLib))
                        return localLib;
                }

                // 2. try current directory
                if (CheckLibraryPath(Directory.GetCurrentDirectory(), arch, libWithExt, out var lib))
                    return lib;

                // 3. try app domain
                try
                {
                    if (AppDomain.CurrentDomain is AppDomain domain)
                    {
                        // 3.1 RelativeSearchPath
                        if (CheckLibraryPath(domain.RelativeSearchPath, arch, libWithExt, out lib))
                            return lib;

                        // 3.2 BaseDirectory
                        if (CheckLibraryPath(domain.BaseDirectory, arch, libWithExt, out lib))
                            return lib;
                    }
                }
                catch
                {
                    // no-op as there may not be any domain or path
                }

                // 4. use PATH or default loading mechanism
                return libWithExt;
            }

            static bool CheckLibraryPath(string root, string arch, string libWithExt, out string foundPath)
            {
                if (!string.IsNullOrEmpty(root))
                {
                    // b. in generic platform sub dir
                    var searchLib = Path.Combine(root, arch, libWithExt);
                    if (File.Exists(searchLib))
                    {
                        foundPath = searchLib;
                        return true;
                    }

                    // c. in root
                    searchLib = Path.Combine(root, libWithExt);
                    if (File.Exists(searchLib))
                    {
                        foundPath = searchLib;
                        return true;
                    }
                }

                // d. nothing
                foundPath = null;
                return false;
            }
        }

        public static IntPtr LoadLibrary(string libraryName)
        {
            if (string.IsNullOrEmpty(libraryName))
                throw new ArgumentNullException(nameof(libraryName));

            IntPtr handle = Win32.LoadLibrary(libraryName);

            return handle;
        }

        public static void FreeLibrary(IntPtr library)
        {
            if (library == IntPtr.Zero)
                return;

            Win32.FreeLibrary(library);
        }

        private static class Win32
        {
            private const string SystemLibrary = "Kernel32.dll";

            [DllImport(SystemLibrary, SetLastError = true, CharSet = CharSet.Ansi)]
            public static extern IntPtr LoadLibrary(string lpFileName);

            [DllImport(SystemLibrary, SetLastError = true, CharSet = CharSet.Ansi)]
            public static extern void FreeLibrary(IntPtr hModule);
        }
    }
#endif
}
