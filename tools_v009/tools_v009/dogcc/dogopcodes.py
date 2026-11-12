#!/usr/bin/env python3
"""dogopcodes.py - DogCode VM opcode metadata recovered from DOGCodeCompiler.exe.

The compiler serializes each emitted parse node as:
    u8 opcode
    followed by (len[op]-1)/4 operand dwords (little endian u32).
len[op] and serializable-flag were extracted from the gOpCodes-style table at
image RVA 0x20030 (12 bytes/opcode) in DOGCodeCompiler.exe.

Instruction layout sanity-checked against the 694-file vanilla corpus and 70+
oracle probes.
"""
import struct

# opcode names in numeric order (enum order confirmed by the writer: opcode
# value == index)
NAMES = [
 "INVALID","NOP","ARG","STATEMENT","JUMPTARGET","WHILE","GOENDWHILE","ENDWHILE",
 "GOTO","CALL","STOP","IF","ELSEIF","ELSE","ENDIF","RETURN","RETURNVAL",
 "DISCARD","FOR","SKIP","NEXT_SYM","PLUS_PLUS","MINUS_MINUS","PLUS_EQUALS",
 "MINUS_EQUALS","MULT_EQUALS","DIV_EQUALS","OR_EQUALS","AND_EQUALS","XOR_EQUALS",
 "MOD_EQUALS","SWITCH","ENDSWITCH","CASE","BREAK","CONTINUE","DEFAULT","INT",
 "FLOAT","STRING","VECTOR","ARTICLE","PUSHINT","PUSHFLOAT","PUSHSTRING",
 "PUSHVECTOR","PUSHSYMBOL","POPINT","POPFLOAT","POPSTRING","POPVECTOR",
 "POPSYMBOL","ARRAYREF","CALLFUNC","UNEG","LNEG","MULTIPLY","DIVIDE","MOD",
 "ADD","SUB","LT","GT","LE","GE","EQ","NE","AND","XOR","OR","LAND","LOR",
 "ASSIGN","GOTRUE","FUNCLABEL","GOTOLABEL","BRA_ENDIF","LINE",
 "ALLOC_INT_ARRAY","ALLOC_FLOAT_ARRAY","ALLOC_ARRAY","ALLOC_ARTICLE",
 "PRINT_ID","PRINT_STRING",
]

# extracted from the exe: (total instruction length, serializable flag)
LEN = {
 0:1,1:1,2:1,3:1,4:1,5:1,6:5,7:5,8:5,9:13,10:1,11:5,12:5,13:1,14:1,15:1,16:1,
 17:1,18:1,19:5,20:5,21:1,22:1,23:1,24:1,25:1,26:1,27:1,28:1,29:1,30:1,31:1,
 32:5,33:5,34:5,35:5,36:1,37:5,38:5,39:5,40:5,41:5,42:5,43:5,44:5,45:13,46:5,
 47:5,48:5,49:5,50:13,51:5,52:1,53:13,54:1,55:1,56:1,57:1,58:1,59:1,60:1,
 61:1,62:1,63:1,64:1,65:1,66:1,67:1,68:1,69:1,70:1,71:1,72:1,73:1,74:5,75:5,
 76:5,77:5,78:1,79:1,80:1,81:1,82:5,83:5,
}
FLAG = {  # 1 = this opcode is serialized (write opcode byte + args)
 5:1,6:1,7:1,8:1,9:1,10:1,11:1,12:1,13:1,14:1,15:1,16:1,17:1,18:1,19:1,20:1,
 21:1,22:1,23:1,24:1,25:1,26:1,27:1,28:1,29:1,30:1,31:1,32:1,33:1,34:1,35:1,
 36:1,37:1,38:1,39:1,40:1,41:1,42:1,43:1,44:1,45:1,46:1,47:1,48:1,49:1,50:1,
 51:1,52:1,53:1,54:1,55:1,56:1,57:1,58:1,59:1,60:1,61:1,62:1,63:1,64:1,65:1,
 66:1,67:1,68:1,69:1,70:1,71:1,72:1,73:1,74:1,75:1,76:1,77:1,78:1,79:1,80:1,
 81:1,82:1,83:1,
}

def args_of(op):
    """number of operand dwords after the opcode byte."""
    return (LEN[op] - 1) // 4

def name_of(op):
    return NAMES[op] if op < len(NAMES) else "OP_%02X" % op

def disassemble(code: bytes, offset=0):
    """Yield (start_off, opcode, operands(list of u32)) for an EDOC code blob."""
    i = 0
    out = []
    n = len(code)
    while i < n:
        op = code[i]
        ln = LEN.get(op)
        if ln is None:
            out.append((offset + i, op, []))
            i += 1
            continue
        operands = [struct.unpack_from("<I", code, i + 1 + 4 * k)[0]
                    for k in range((ln - 1) // 4)]
        out.append((offset + i, op, operands))
        i += ln
    return out
