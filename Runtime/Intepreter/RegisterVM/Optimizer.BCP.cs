﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using ILRuntime.Mono.Cecil;
using ILRuntime.Mono.Cecil.Cil;

using ILRuntime.CLR.TypeSystem;
using ILRuntime.CLR.Method;
using ILRuntime.Runtime.Intepreter.OpCodes;

namespace ILRuntime.Runtime.Intepreter.RegisterVM
{
    partial class Optimizer
    {
        public static void BackwardsCopyPropagation(List<CodeBasicBlock> blocks, bool hasReturn,short stackRegisterBegin)
        {
            foreach (var b in blocks)
            {
                var lst = b.FinalInstructions;
                HashSet<int> canRemove = b.CanRemove;
                //HashSet<int> pendingBCP = b.PendingCP;
                bool isInline = false;

                for (int i = lst.Count - 1; i >= 0; i--)
                {
                    if (canRemove.Contains(i))
                        continue;
                    OpCodeR X = lst[i];
                    if (X.Code == OpCodeREnum.InlineStart)
                    {
                        isInline = false;
                        continue;
                    }
                    if (X.Code == OpCodeREnum.InlineEnd)
                    {
                        isInline = true;
                        continue;
                    }
                    //if (isInline)
                    //    continue;
                    if (X.Code == OpCodeREnum.Nop)
                    {
                        canRemove.Add(i);
                        continue;
                    }
                    if (X.Code == OpCodeREnum.Move)
                    {
                        short xSrc, xSrc2, xSrc3, xDst;
                        GetOpcodeSourceRegister(ref X, hasReturn, out xSrc, out xSrc2, out xSrc3);
                        GetOpcodeDestRegister(ref X, out xDst);
                        if (xDst == xSrc)
                        {
                            canRemove.Add(i);
                            continue;
                        }
                        //Only deal with stack->local
                        if (xSrc < stackRegisterBegin || xDst >= stackRegisterBegin || isInline)
                            continue;
                        bool ended = false;
                        bool propagationInline = false;
                        for (int j = i - 1; j >= 0; j--)
                        {
                            OpCodeR Y = lst[j];
                            if (Y.Code == OpCodeREnum.InlineStart)
                                propagationInline = false;
                            else if (Y.Code == OpCodeREnum.InlineEnd)
                            {
                                propagationInline = true;
                            }
                            short yDst;
                            if (GetOpcodeDestRegister(ref Y, out yDst))
                            {
                                if (xDst == yDst && !propagationInline)
                                {
                                    ended = true;
                                    break;
                                }
                                if (xSrc == yDst)
                                {
                                    if (propagationInline)
                                    {
                                        ended = true;
                                        break;
                                    }
                                    ReplaceOpcodeDest(ref Y, xDst);
                                    for (int k = j + 1; k < i; k++)
                                    {
                                        if (canRemove.Contains(k))
                                            continue;
                                        OpCodeR Z = lst[k];
                                        bool replaced = false;
                                        short zSrc, zSrc2, zSrc3;
                                        if (GetOpcodeSourceRegister(ref Z, hasReturn, out zSrc, out zSrc2, out zSrc3))
                                        {
                                            if(zSrc == yDst)
                                            {
                                                replaced = true;
                                                ReplaceOpcodeSource(ref Z, 0, xDst);
                                            }
                                            if (zSrc2 == yDst)
                                            {
                                                replaced = true;
                                                ReplaceOpcodeSource(ref Z, 1, xDst);
                                            }
                                            if (zSrc3 == yDst)
                                            {
                                                replaced = true;
                                                ReplaceOpcodeSource(ref Z, 2, xDst);
                                            }
                                        }
                                        if (replaced)
                                            lst[k] = Z;
                                    }
                                    canRemove.Add(i);
                                    ended = true;
                                    lst[j] = Y;
                                    break;
                                }
                            }
                            short ySrc, ySrc2, ySrc3;
                            if (GetOpcodeSourceRegister(ref Y, hasReturn, out ySrc, out ySrc2, out ySrc3))
                            {
                                if (ySrc >= 0 && ySrc == xDst)
                                {
                                    ended = true;
                                    break;
                                }
                                if (ySrc2 >= 0 && ySrc2 == xDst)
                                {
                                    ended = true;
                                    break;
                                }
                                if (ySrc3 >= 0 && ySrc3 == xDst)
                                {
                                    ended = true;
                                    break;
                                }
                            }
                        }

                        /*if (!ended)
                        {
                            if (xDst < stackRegisterBegin)
                            {
                                pendingBCP.Add(i);
                                throw new NotImplementedException();
                            }
                        }*/
                    }
                }
            }
        }
    }
}
