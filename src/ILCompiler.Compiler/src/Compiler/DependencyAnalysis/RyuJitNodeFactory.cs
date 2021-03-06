﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Internal.TypeSystem;

using Debug = System.Diagnostics.Debug;

namespace ILCompiler.DependencyAnalysis
{
    public sealed class RyuJitNodeFactory : NodeFactory
    {
        public RyuJitNodeFactory(CompilerTypeSystemContext context, CompilationModuleGroup compilationModuleGroup)
            : base(context, compilationModuleGroup, new CompilerGeneratedMetadataManager(compilationModuleGroup, context), new CoreRTNameMangler(false))
        {
        }

        protected override IMethodNode CreateMethodEntrypointNode(MethodDesc method)
        {
            if (method.IsInternalCall)
            {
                if (TypeSystemContext.IsSpecialUnboxingThunkTargetMethod(method))
                {
                    return MethodEntrypoint(TypeSystemContext.GetRealSpecialUnboxingThunkTargetMethod(method));
                }
                else if (method.IsArrayAddressMethod())
                {
                    return MethodEntrypoint(((ArrayType)method.OwningType).GetArrayMethod(ArrayMethodKind.AddressWithHiddenArg));
                }
                else if (method.HasCustomAttribute("System.Runtime", "RuntimeImportAttribute"))
                {
                    return new RuntimeImportMethodNode(method);
                }

                // On CLR this would throw a SecurityException with "ECall methods must be packaged into a system module."
                // This is a corner case that nobody is likely to care about.
                throw new TypeSystemException.InvalidProgramException(ExceptionStringID.InvalidProgramSpecific, method);
            }

            if (CompilationModuleGroup.ContainsMethod(method))
            {
                return new MethodCodeNode(method);
            }
            else
            {
                return new ExternMethodSymbolNode(this, method);
            }
        }

        protected override IMethodNode CreateUnboxingStubNode(MethodDesc method)
        {
            Debug.Assert(!method.Signature.IsStatic);

            if (method.IsCanonicalMethod(CanonicalFormKind.Specific) && !method.HasInstantiation)
            {
                // Unboxing stubs to canonical instance methods need a special unboxing stub that unboxes
                // 'this' and also provides an instantiation argument (we do a calling convention conversion).
                // We don't do this for generic instance methods though because they don't use the EEType
                // for the generic context anyway.
                return new UnboxingThunkMethodCodeNode(TypeSystemContext.GetSpecialUnboxingThunk(method, CompilationModuleGroup.GeneratedAssembly), method);
            }
            else
            {
                // Otherwise we just unbox 'this' and don't touch anything else.
                return new UnboxingStubNode(method);
            }
        }

        protected override ISymbolNode CreateReadyToRunHelperNode(Tuple<ReadyToRunHelperId, object> helperCall)
        {
            return new ReadyToRunHelperNode(this, helperCall.Item1, helperCall.Item2);
        }

        protected override IMethodNode CreateShadowConcreteMethodNode(MethodKey methodKey)
        {
            return new ShadowConcreteMethodNode(methodKey.Method, 
                MethodEntrypoint(
                    methodKey.Method.GetCanonMethodTarget(CanonicalFormKind.Specific),
                    methodKey.IsUnboxingStub));
        }
    }
}
