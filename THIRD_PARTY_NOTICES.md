# Third-party notices

Notices of third-party libraries that the published Obj2Tiles binaries include, under their own licences.

## LibTiff.Net 2.4.660

Reads the TIFF textures whose samples are wider than 8 bits (the reflectance of multispectral and thermal models). https://github.com/BitMiracle/libtiff.net, BSD 3-Clause licence:

```
LibTiff.Net

Copyright (c) 2008-2023 Bit Miracle
http://www.bitmiracle.com
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are
met:
  Redistributions of source code must retain the above copyright notice,
  this list of conditions and the following disclaimer.

  Redistributions in binary form must reproduce the above copyright
  notice, this list of conditions and the following disclaimer in the
  documentation and/or other materials provided with the distribution.

  Neither the name of the Bit Miracle nor the names of its contributors
  may be used to endorse or promote products derived from this software
  without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS
IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A
PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL BIT MIRACLE BE
LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF
THE POSSIBILITY OF SUCH DAMAGE.


This software is based in part on the
The Independent JPEG Group's JPEG software
http://www.ijg.org/

Copyright (C) 1991-1998, Thomas G. Lane.


This software is based in part on the
LibTIFF - TIFF Library and Utilities
http://www.remotesensing.org/libtiff/

Copyright (c) 1988-1997 Sam Leffler
Copyright (c) 1991-1997 Silicon Graphics, Inc.


Portions Copyright (C) 2006, ComponentAce
http://www.componentace.com
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are
met:
  Redistributions of source code must retain the above copyright notice,
  this list of conditions and the following disclaimer.

  Redistributions in binary form must reproduce the above copyright
  notice, this list of conditions and the following disclaimer in the
  documentation and/or other materials provided with the distribution.

  Neither the name of ComponentAce nor the names of its contributors
  may be used to endorse or promote products derived from this software
  without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS
IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED
TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A
PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED
TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF
LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING
NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

## KTX-Software (libktx) 5.0.0-rc2

Encodes the KTX2 textures (`--texture-format Ktx2`). The native library libktx (`ktx.dll`, `libktx.so`, `libktx.dylib`) is the one of the KTX-Software 5.0.0-rc2 release packages, shipped unmodified next to the executable. https://github.com/KhronosGroup/KTX-Software, Apache License 2.0 (text at the end of this file):

```
Copyright 2013-2020 Mark Callow, The Khronos Group Inc. and the contributors to the KTX project.

Files unique to the KTX-Software repository generally fall under the Apache 2.0 license
with copyright holders including Mark Callow, the KTX-Software author; The
Khronos Group Inc., which has supported KTX development; and other
contributors to the KTX project.
```

libktx is built from the following code of other projects, under their own licences.

### Data Format Descriptor utilities (dfdutils)

https://github.com/KhronosGroup/dfdutils, Apache License 2.0 (text at the end of this file):

```
Copyright 2019-2020 The Khronos Group Inc.
```

### astc-encoder

Encodes ASTC blocks. https://github.com/ARM-software/astc-encoder, Apache License 2.0 (text at the end of this file):

```
Copyright 2020-2026 Arm Limited
```

### Ericsson Texture Compression Codec (etcdec)

Decodes ETC blocks (libktx's software ETC unpacking). Not open source: it is available under the Ericsson software licence agreement below.

```
(C) Ericsson AB 2013. All Rights Reserved.

Software License Agreement

PLEASE REVIEW THE FOLLOWING TERMS AND CONDITIONS PRIOR TO USING THE
ERICSSON TEXTURE COMPRESSION CODEC SOFTWARE (THE "SOFTWARE"). THE USE
OF THE SOFTWARE IS SUBJECT TO THE TERMS AND CONDITIONS OF THE
FOLLOWING SOFTWARE LICENSE AGREEMENT (THE "SLA"). IF YOU DO NOT ACCEPT
SUCH TERMS AND CONDITIONS YOU MAY NOT USE THE SOFTWARE.

Subject to the terms and conditions of the SLA, the licensee of the
Software (the "Licensee") hereby, receives a non-exclusive,
non-transferable, limited, free-of-charge, perpetual and worldwide
license, to copy, use, distribute and modify the Software, but only
for the purpose of developing, manufacturing, selling, using and
distributing products including the Software in binary form, which
products are used for compression and/or decompression according to
the Khronos standard specifications OpenGL, OpenGL ES and
WebGL. Notwithstanding anything of the above, Licensee may distribute
[etcdec.cxx] in source code form provided (i) it is in unmodified
form; and (ii) it is included in software owned by Licensee.

If Licensee institutes, or threatens to institute, patent litigation
against Ericsson or Ericsson's affiliates for using the Software for
developing, having developed, manufacturing, having manufactured,
selling, offer for sale, importing, using, leasing, operating,
repairing and/or distributing products (i) within the scope of the
Khronos framework; or (ii) using software or other intellectual
property rights owned by Ericsson or its affiliates and provided under
the Khronos framework, Ericsson shall have the right to terminate this
SLA with immediate effect. Moreover, if Licensee institutes, or
threatens to institute, patent litigation against any other licensee
of the Software for using the Software in products within the scope of
the Khronos framework, Ericsson shall have the right to terminate this
SLA with immediate effect. However, should Licensee institute, or
threaten to institute, patent litigation against any other licensee of
the Software based on such other licensee's use of any other software
together with the Software, then Ericsson shall have no right to
terminate this SLA.

This SLA does not transfer to Licensee any ownership to any Ericsson
or third party intellectual property rights. All rights not expressly
granted by Ericsson under this SLA are hereby expressly
reserved. Furthermore, nothing in this SLA shall be construed as a
right to use or sell products in a manner which conveys or purports to
convey whether explicitly, by principles of implied license, or
otherwise, any rights to any third party, under any patent of Ericsson
or of Ericsson's affiliates covering or relating to any combination of
the Software with any other software or product (not licensed
hereunder) where the right applies specifically to the combination and
not to the software or product itself.

THE SOFTWARE IS PROVIDED "AS IS". ERICSSON MAKES NO REPRESENTATIONS OF
ANY KIND, EXTENDS NO WARRANTIES OR CONDITIONS OF ANY KIND, EITHER
EXPRESS, IMPLIED OR STATUTORY; INCLUDING, BUT NOT LIMITED TO, EXPRESS,
IMPLIED OR STATUTORY WARRANTIES OR CONDITIONS OF TITLE,
MERCHANTABILITY, SATISFACTORY QUALITY, SUITABILITY, AND FITNESS FOR A
PARTICULAR PURPOSE. THE ENTIRE RISK AS TO THE QUALITY AND PERFORMANCE
OF THE SOFTWARE IS WITH THE LICENSEE. SHOULD THE SOFTWARE PROVE
DEFECTIVE, THE LICENSEE ASSUMES THE COST OF ALL NECESSARY SERVICING,
REPAIR OR CORRECTION. ERICSSON MAKES NO WARRANTY THAT THE MANUFACTURE,
SALE, OFFERING FOR SALE, DISTRIBUTION, LEASE, USE OR IMPORTATION UNDER
THE SLA WILL BE FREE FROM INFRINGEMENT OF PATENTS, COPYRIGHTS OR OTHER
INTELLECTUAL PROPERTY RIGHTS OF OTHERS, AND THE VALIDITY OF THE
LICENSE AND THE SLA ARE SUBJECT TO LICENSEE'S SOLE RESPONSIBILITY TO
MAKE SUCH DETERMINATION AND ACQUIRE SUCH LICENSES AS MAY BE NECESSARY
WITH RESPECT TO PATENTS, COPYRIGHT AND OTHER INTELLECTUAL PROPERTY OF
THIRD PARTIES.

THE LICENSEE ACKNOWLEDGES AND ACCEPTS THAT THE SOFTWARE (I) IS NOT
LICENSED FOR; (II) IS NOT DESIGNED FOR OR INTENDED FOR; AND (III) MAY
NOT BE USED FOR; ANY MISSION CRITICAL APPLICATIONS SUCH AS, BUT NOT
LIMITED TO OPERATION OF NUCLEAR OR HEALTHCARE COMPUTER SYSTEMS AND/OR
NETWORKS, AIRCRAFT OR TRAIN CONTROL AND/OR COMMUNICATION SYSTEMS OR
ANY OTHER COMPUTER SYSTEMS AND/OR NETWORKS OR CONTROL AND/OR
COMMUNICATION SYSTEMS ALL IN WHICH CASE THE FAILURE OF THE SOFTWARE
COULD LEAD TO DEATH, PERSONAL INJURY, OR SEVERE PHYSICAL, MATERIAL OR
ENVIRONMENTAL DAMAGE. LICENSEE'S RIGHTS UNDER THIS LICENSE WILL
TERMINATE AUTOMATICALLY AND IMMEDIATELY WITHOUT NOTICE IF LICENSEE
FAILS TO COMPLY WITH THIS PARAGRAPH.

IN NO EVENT SHALL ERICSSON BE LIABLE FOR ANY DAMAGES WHATSOEVER,
INCLUDING BUT NOT LIMITED TO PERSONAL INJURY, ANY GENERAL, SPECIAL,
INDIRECT, INCIDENTAL OR CONSEQUENTIAL DAMAGES, ARISING OUT OF OR IN
CONNECTION WITH THE USE OR INABILITY TO USE THE SOFTWARE (INCLUDING
BUT NOT LIMITED TO LOSS OF PROFITS, BUSINESS INTERUPTIONS, OR ANY
OTHER COMMERCIAL DAMAGES OR LOSSES, LOSS OF DATA OR DATA BEING
RENDERED INACCURATE OR LOSSES SUSTAINED BY THE LICENSEE OR THIRD
PARTIES OR A FAILURE OF THE SOFTWARE TO OPERATE WITH ANY OTHER
SOFTWARE) REGARDLESS OF THE THEORY OF LIABILITY (CONTRACT, TORT, OR
OTHERWISE), EVEN IF THE LICENSEE OR ANY OTHER PARTY HAS BEEN ADVISED
OF THE POSSIBILITY OF SUCH DAMAGES.

Licensee acknowledges that "ERICSSON ///" is the corporate trademark
of Telefonaktiebolaget LM Ericsson and that both "Ericsson" and the
figure "///" are important features of the trade names of
Telefonaktiebolaget LM Ericsson. Nothing contained in these terms and
conditions shall be deemed to grant Licensee any right, title or
interest in the word "Ericsson" or the figure "///". No delay or
omission by Ericsson to exercise any right or power shall impair any
such right or power to be construed to be a waiver thereof. Consent by
Ericsson to, or waiver of, a breach by the Licensee shall not
constitute consent to, waiver of, or excuse for any other different or
subsequent breach.

This SLA shall be governed by the substantive law of Sweden. Any
dispute, controversy or claim arising out of or in connection with
this SLA, or the breach, termination or invalidity thereof, shall be
submitted to the exclusive jurisdiction of the Swedish Courts.
```

### Basis Universal

Encodes the Basis Universal (ETC1S/BasisLZ and UASTC) textures. https://github.com/BinomialLLC/basis_universal, Apache License 2.0 (text at the end of this file). The attribution notices of its NOTICE file:

```
NOTICE

Basis Universal™ Supercompressed GPU Texture Compression Library

Copyright © 2016–2026 Binomial LLC. 
All rights reserved except as granted under the [Apache 2.0 license](https://github.com/BinomialLLC/basis_universal/blob/master/LICENSE).
"Basis Universal" is a trademark of Binomial LLC.

The documents in the Basis Universal wiki, and the Basis Universal library, example, and tool source code, fall under the Apache 2.0 license, unless otherwise explicitly indicated. 

Redistributions or derivative works must include a readable copy of the attribution notices from this NOTICE file (see Apache License 2.0 § 4(d)).

If you modify the Basis Universal source code, specifications, or wiki documents and redistribute the files, you must cause any modified files to carry prominent notices stating that you changed the files (see Apache 2.0 §4(b)).

**This software, documentation and specifications are provided "as is", without warranty of any kind (see Apache 2.0 §§7–8).**
```

Basis Universal includes the following code of other projects.

#### Android ASTC decoder (drawElements Quality Program)

Apache License 2.0 (text at the end of this file):

```
Copyright 2016 The Android Open Source Project
```

#### Zstandard

https://github.com/facebook/zstd, BSD licence:

```
BSD License

For Zstandard software

Copyright (c) 2016-present, Facebook, Inc. All rights reserved.

Redistribution and use in source and binary forms, with or without modification,
are permitted provided that the following conditions are met:

 * Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.

 * Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

 * Neither the name Facebook nor the names of its contributors may be used to
   endorse or promote products derived from this software without specific
   prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON
ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

#### TinyEXR

https://github.com/syoyo/tinyexr, BSD 3-Clause licence:

```
Copyright (c) 2014 - 2021, Syoyo Fujita and many contributors.
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:
    * Redistributions of source code must retain the above copyright
      notice, this list of conditions and the following disclaimer.
    * Redistributions in binary form must reproduce the above copyright
      notice, this list of conditions and the following disclaimer in the
      documentation and/or other materials provided with the distribution.
    * Neither the name of the Syoyo Fujita nor the
      names of its contributors may be used to endorse or promote products
      derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL <COPYRIGHT HOLDER> BE LIABLE FOR ANY
DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
```

TinyEXR contains code of OpenEXR, under this licence:

```
Copyright (c) 2002, Industrial Light & Magic, a division of Lucas
Digital Ltd. LLC

All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are
met:
*       Redistributions of source code must retain the above copyright
notice, this list of conditions and the following disclaimer.
*       Redistributions in binary form must reproduce the above
copyright notice, this list of conditions and the following disclaimer
in the documentation and/or other materials provided with the
distribution.
*       Neither the name of Industrial Light & Magic nor the names of
its contributors may be used to endorse or promote products derived
from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
"AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

```

#### QOI

https://github.com/phoboslab/qoi, MIT licence:

```
Copyright (c) 2021, Dominic Szablewski - https://phoboslab.org

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

#### tiny_dds

https://github.com/DeanoC/tiny_dds, MIT licence:

```
MIT License

Copyright (c) 2019 DeanoC

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

#### jpgd and pvpngreader

JPEG and PNG readers by Richard Geldreich, Jr., released into the public domain:

```
This is free and unencumbered software released into the public domain.

Anyone is free to copy, modify, publish, use, compile, sell, or
distribute this software, either in source code form or as a compiled
binary, for any purpose, commercial or non-commercial, and by any
means.

In jurisdictions that recognize copyright laws, the author or authors
of this software dedicate any and all copyright interest in the
software to the public domain. We make this dedication for the benefit
of the public at large and to the detriment of our heirs and
successors. We intend this dedication to be an overt act of
relinquishment in perpetuity of all present and future rights to this
software under copyright law.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR
OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
OTHER DEALINGS IN THE SOFTWARE.

For more information, please refer to <http://unlicense.org/>
```

## Apache License 2.0

The licence of KTX-Software, dfdutils, astc-encoder, Basis Universal and the Android ASTC decoder:

```
                                 Apache License
                           Version 2.0, January 2004
                        http://www.apache.org/licenses/

   TERMS AND CONDITIONS FOR USE, REPRODUCTION, AND DISTRIBUTION

   1. Definitions.

      "License" shall mean the terms and conditions for use, reproduction,
      and distribution as defined by Sections 1 through 9 of this document.

      "Licensor" shall mean the copyright owner or entity authorized by
      the copyright owner that is granting the License.

      "Legal Entity" shall mean the union of the acting entity and all
      other entities that control, are controlled by, or are under common
      control with that entity. For the purposes of this definition,
      "control" means (i) the power, direct or indirect, to cause the
      direction or management of such entity, whether by contract or
      otherwise, or (ii) ownership of fifty percent (50%) or more of the
      outstanding shares, or (iii) beneficial ownership of such entity.

      "You" (or "Your") shall mean an individual or Legal Entity
      exercising permissions granted by this License.

      "Source" form shall mean the preferred form for making modifications,
      including but not limited to software source code, documentation
      source, and configuration files.

      "Object" form shall mean any form resulting from mechanical
      transformation or translation of a Source form, including but
      not limited to compiled object code, generated documentation,
      and conversions to other media types.

      "Work" shall mean the work of authorship, whether in Source or
      Object form, made available under the License, as indicated by a
      copyright notice that is included in or attached to the work
      (an example is provided in the Appendix below).

      "Derivative Works" shall mean any work, whether in Source or Object
      form, that is based on (or derived from) the Work and for which the
      editorial revisions, annotations, elaborations, or other modifications
      represent, as a whole, an original work of authorship. For the purposes
      of this License, Derivative Works shall not include works that remain
      separable from, or merely link (or bind by name) to the interfaces of,
      the Work and Derivative Works thereof.

      "Contribution" shall mean any work of authorship, including
      the original version of the Work and any modifications or additions
      to that Work or Derivative Works thereof, that is intentionally
      submitted to Licensor for inclusion in the Work by the copyright owner
      or by an individual or Legal Entity authorized to submit on behalf of
      the copyright owner. For the purposes of this definition, "submitted"
      means any form of electronic, verbal, or written communication sent
      to the Licensor or its representatives, including but not limited to
      communication on electronic mailing lists, source code control systems,
      and issue tracking systems that are managed by, or on behalf of, the
      Licensor for the purpose of discussing and improving the Work, but
      excluding communication that is conspicuously marked or otherwise
      designated in writing by the copyright owner as "Not a Contribution."

      "Contributor" shall mean Licensor and any individual or Legal Entity
      on behalf of whom a Contribution has been received by Licensor and
      subsequently incorporated within the Work.

   2. Grant of Copyright License. Subject to the terms and conditions of
      this License, each Contributor hereby grants to You a perpetual,
      worldwide, non-exclusive, no-charge, royalty-free, irrevocable
      copyright license to reproduce, prepare Derivative Works of,
      publicly display, publicly perform, sublicense, and distribute the
      Work and such Derivative Works in Source or Object form.

   3. Grant of Patent License. Subject to the terms and conditions of
      this License, each Contributor hereby grants to You a perpetual,
      worldwide, non-exclusive, no-charge, royalty-free, irrevocable
      (except as stated in this section) patent license to make, have made,
      use, offer to sell, sell, import, and otherwise transfer the Work,
      where such license applies only to those patent claims licensable
      by such Contributor that are necessarily infringed by their
      Contribution(s) alone or by combination of their Contribution(s)
      with the Work to which such Contribution(s) was submitted. If You
      institute patent litigation against any entity (including a
      cross-claim or counterclaim in a lawsuit) alleging that the Work
      or a Contribution incorporated within the Work constitutes direct
      or contributory patent infringement, then any patent licenses
      granted to You under this License for that Work shall terminate
      as of the date such litigation is filed.

   4. Redistribution. You may reproduce and distribute copies of the
      Work or Derivative Works thereof in any medium, with or without
      modifications, and in Source or Object form, provided that You
      meet the following conditions:

      (a) You must give any other recipients of the Work or
          Derivative Works a copy of this License; and

      (b) You must cause any modified files to carry prominent notices
          stating that You changed the files; and

      (c) You must retain, in the Source form of any Derivative Works
          that You distribute, all copyright, patent, trademark, and
          attribution notices from the Source form of the Work,
          excluding those notices that do not pertain to any part of
          the Derivative Works; and

      (d) If the Work includes a "NOTICE" text file as part of its
          distribution, then any Derivative Works that You distribute must
          include a readable copy of the attribution notices contained
          within such NOTICE file, excluding those notices that do not
          pertain to any part of the Derivative Works, in at least one
          of the following places: within a NOTICE text file distributed
          as part of the Derivative Works; within the Source form or
          documentation, if provided along with the Derivative Works; or,
          within a display generated by the Derivative Works, if and
          wherever such third-party notices normally appear. The contents
          of the NOTICE file are for informational purposes only and
          do not modify the License. You may add Your own attribution
          notices within Derivative Works that You distribute, alongside
          or as an addendum to the NOTICE text from the Work, provided
          that such additional attribution notices cannot be construed
          as modifying the License.

      You may add Your own copyright statement to Your modifications and
      may provide additional or different license terms and conditions
      for use, reproduction, or distribution of Your modifications, or
      for any such Derivative Works as a whole, provided Your use,
      reproduction, and distribution of the Work otherwise complies with
      the conditions stated in this License.

   5. Submission of Contributions. Unless You explicitly state otherwise,
      any Contribution intentionally submitted for inclusion in the Work
      by You to the Licensor shall be under the terms and conditions of
      this License, without any additional terms or conditions.
      Notwithstanding the above, nothing herein shall supersede or modify
      the terms of any separate license agreement you may have executed
      with Licensor regarding such Contributions.

   6. Trademarks. This License does not grant permission to use the trade
      names, trademarks, service marks, or product names of the Licensor,
      except as required for reasonable and customary use in describing the
      origin of the Work and reproducing the content of the NOTICE file.

   7. Disclaimer of Warranty. Unless required by applicable law or
      agreed to in writing, Licensor provides the Work (and each
      Contributor provides its Contributions) on an "AS IS" BASIS,
      WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
      implied, including, without limitation, any warranties or conditions
      of TITLE, NON-INFRINGEMENT, MERCHANTABILITY, or FITNESS FOR A
      PARTICULAR PURPOSE. You are solely responsible for determining the
      appropriateness of using or redistributing the Work and assume any
      risks associated with Your exercise of permissions under this License.

   8. Limitation of Liability. In no event and under no legal theory,
      whether in tort (including negligence), contract, or otherwise,
      unless required by applicable law (such as deliberate and grossly
      negligent acts) or agreed to in writing, shall any Contributor be
      liable to You for damages, including any direct, indirect, special,
      incidental, or consequential damages of any character arising as a
      result of this License or out of the use or inability to use the
      Work (including but not limited to damages for loss of goodwill,
      work stoppage, computer failure or malfunction, or any and all
      other commercial damages or losses), even if such Contributor
      has been advised of the possibility of such damages.

   9. Accepting Warranty or Additional Liability. While redistributing
      the Work or Derivative Works thereof, You may choose to offer,
      and charge a fee for, acceptance of support, warranty, indemnity,
      or other liability obligations and/or rights consistent with this
      License. However, in accepting such obligations, You may act only
      on Your own behalf and on Your sole responsibility, not on behalf
      of any other Contributor, and only if You agree to indemnify,
      defend, and hold each Contributor harmless for any liability
      incurred by, or claims asserted against, such Contributor by reason
      of your accepting any such warranty or additional liability.

   END OF TERMS AND CONDITIONS

   APPENDIX: How to apply the Apache License to your work.

      To apply the Apache License to your work, attach the following
      boilerplate notice, with the fields enclosed by brackets "[]"
      replaced with your own identifying information. (Don't include
      the brackets!)  The text should be enclosed in the appropriate
      comment syntax for the file format. We also recommend that a
      file or class name and description of purpose be included on the
      same "printed page" as the copyright notice for easier
      identification within third-party archives.

   Copyright 2019-2026 Binomial LLC

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
```
