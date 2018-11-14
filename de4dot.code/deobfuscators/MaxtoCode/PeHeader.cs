﻿/*
    Copyright (C) 2011-2015 de4dot@gmail.com

    This file is part of de4dot.

    de4dot is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    de4dot is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with de4dot.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using dnlib.PE;

namespace de4dot.code.deobfuscators.MaxtoCode {
	enum EncryptionVersion {
		Unknown,
		V1,
		V2,
		V3,
		V4,
		V5,
		V6,
		V7,
		V8,
	}

	class PeHeader {
		EncryptionVersion version;
		byte[] headerData;
		uint xorKey;

		public EncryptionVersion EncryptionVersion => version;

		public PeHeader(MainType mainType, MyPEImage peImage) {
			version = GetHeaderOffsetAndVersion(peImage, out uint headerOffset);
			headerData = peImage.OffsetReadBytes(headerOffset, 0x1000);

			// Guess the xorKey by the Python script(Pending)
			switch (version) {
			case EncryptionVersion.V1:
			case EncryptionVersion.V2:
			case EncryptionVersion.V3:
			case EncryptionVersion.V4:
			case EncryptionVersion.V5:
			default:
				xorKey = 0x7ABF931;
				break;

			case EncryptionVersion.V6:
				xorKey = 0x7ABA931;
				break;

			case EncryptionVersion.V7:
				xorKey = 0x8ABA931;
				break;

			case EncryptionVersion.V8:
				if (CheckMcKeyRva(peImage, 0x99BA9A13))
					break;
				if (CheckMcKeyRva(peImage, 0x18ABA931))
					break;
				if (CheckMcKeyRva(peImage, 0x18ABA933))
					break;
				break;
			}
		}

		bool CheckMcKeyRva(MyPEImage peImage, uint newXorKey) {
			xorKey = newXorKey;
			uint rva = GetMcKeyRva();
			return (rva & 0xFFF) == 0 && peImage.FindSection((RVA)rva) != null;
		}

		public uint GetMcKeyRva() => GetRva(0x0FFC, xorKey);
		public uint GetRva(int offset, uint xorKey) => ReadUInt32(offset) ^ xorKey;
		public uint ReadUInt32(int offset) => BitConverter.ToUInt32(headerData, offset);

		static EncryptionVersion GetHeaderOffsetAndVersion(MyPEImage peImage, out uint headerOffset) {
			headerOffset = 0;

			var version = GetVersion(peImage, headerOffset);
			if (version != EncryptionVersion.Unknown)
				return version;

			var section = peImage.FindSection(".rsrc");
			if (section != null) {
				version = GetHeaderOffsetAndVersion(section, peImage, out headerOffset);
				if (version != EncryptionVersion.Unknown)
					return version;
			}

			foreach (var section2 in peImage.Sections) {
				version = GetHeaderOffsetAndVersion(section2, peImage, out headerOffset);
				if (version != EncryptionVersion.Unknown)
					return version;
			}

			return EncryptionVersion.Unknown;
		}

		static EncryptionVersion GetHeaderOffsetAndVersion(ImageSectionHeader section, MyPEImage peImage, out uint headerOffset) {
			headerOffset = section.PointerToRawData;
			uint end = section.PointerToRawData + section.SizeOfRawData - 0x1000 + 1;
			while (headerOffset < end) {
				var version = GetVersion(peImage, headerOffset);
				if (version != EncryptionVersion.Unknown)
					return version;
				headerOffset++;
			}

			return EncryptionVersion.Unknown;
		}

		static EncryptionVersion GetVersion(MyPEImage peImage, uint headerOffset) {
			uint m1lo = peImage.OffsetReadUInt32(headerOffset + 0x900);
			uint m1hi = peImage.OffsetReadUInt32(headerOffset + 0x904);

			// Key reader of Rva900h keys, just the possible combinations.
			// Two methods: Call hundreds of staffs to build and run it respectively
			// Or use Automated scripts to run msbuild then run all instances of de4dot

			/*
			if (m1lo > 0 && m1hi > 0) {
			// Print Possible MagicLo from Rva900h
				Logger.vv("The MagicLo from Rva900h could be");
				Logger.Instance.Indent();
				Logger.vv("MagicLo = 0x" + m1lo.ToString("X"));
				Logger.Instance.DeIndent();

			// Print Possible MagicHi from Rva900h			
				Logger.vv("The MagicHi from Rva900h could be");
				Logger.Instance.Indent();
				Logger.vv("MagicHi = 0x" + m1hi.ToString("X"));
				Logger.Instance.DeIndent();
				Logger.vv("_________________________________");
			}
			*/

			foreach (var info in EncryptionInfos.Rva900h) {
				if (info.MagicLo == m1lo && info.MagicHi == m1hi) {
					// Print Successful MagicLo from Rva900h
					Logger.vv("The used MagicLo from Rva900h is");
					Logger.Instance.Indent();
					Logger.vv("MagicLo = 0x" + m1lo.ToString("X"));
					Logger.Instance.DeIndent();

					// Print Successful MagicHi from Rva900h			
					Logger.vv("The used MagicHi from Rva900h is");
					Logger.Instance.Indent();
					Logger.vv("MagicHi = 0x" + m1hi.ToString("X"));
					Logger.Instance.DeIndent();
					Logger.vv("_________________________________");

					Logger.vv("Check these keys in EncryptionInfo[] Rva900h in de4dot.code\\deobfuscators\\MaxtoCode\\EncryptionInfos.cs");

					return info.Version;
				}
			}

			return EncryptionVersion.Unknown;
		}
	}
}
