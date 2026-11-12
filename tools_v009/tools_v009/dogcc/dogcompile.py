#!/usr/bin/env python3
"""dogcompile.py - original reimplementation of DOGCodeCompiler (.scp->.scb).

Byte-exact target. Reverse-engineered rules documented in scb_work/docs/.
Subset implemented so far: functions/params/locals/shared, typed & plain
assignments, literals, binary/unary exprs, plain/member/'call' invocations,
return (expr), if/elseif/else/endif, while/endwhile, goto/labels,
print/printf (ordinary calls), break/continue placeholders.
"""
import os
import struct
import sys
from .doglexer import tokenize
from . import dogopcodes as OC
from . import dogwriter as DW

SHARED_BIT = 0x20000000
BUILTINS = ['self', 'None', 'Null', 'True', 'False']

OP = {
 'WHILE':0x05,'GOENDWHILE':0x06,'ENDWHILE':0x07,'GOTO':0x08,'CALL':0x09,
 'IF':0x0b,'ELSEIF':0x0c,'ELSE':0x0d,'ENDIF':0x0e,'RETURN':0x0f,
 'RETURNVAL':0x10,'DISCARD':0x11,'FOR':0x12,'SKIP':0x13,'NEXT_SYM':0x14,
 'PLUS_PLUS':0x15,'MINUS_MINUS':0x16,'PLUS_EQUALS':0x17,'MINUS_EQUALS':0x18,
 'MULT_EQUALS':0x19,'DIV_EQUALS':0x1a,'OR_EQUALS':0x1b,'AND_EQUALS':0x1c,
 'XOR_EQUALS':0x1d,'MOD_EQUALS':0x1e,'SWITCH':0x1f,
 'ENDSWITCH':0x20,'CASE':0x21,'BREAK':0x22,'CONTINUE':0x23,'DEFAULT':0x24,
 'INT':0x25,'FLOAT':0x26,'STRING':0x27,'VECTOR':0x28,'ARTICLE':0x29,
 'PUSHINT':0x2a,'PUSHFLOAT':0x2b,'PUSHSTRING':0x2c,'PUSHVECTOR':0x2d,
 'PUSHSYMBOL':0x2e,'POPSYMBOL':0x33,'ARRAYREF':0x34,'CALLFUNC':0x35,
 'UNEG':0x36,'LNEG':0x37,'MULTIPLY':0x38,'DIVIDE':0x39,'MOD':0x3a,'ADD':0x3b,
 'SUB':0x3c,'LT':0x3d,'GT':0x3e,'LE':0x3f,'GE':0x40,'EQ':0x41,'NE':0x42,
 'AND':0x43,'XOR':0x44,'OR':0x45,'LAND':0x46,'LOR':0x47,'ASSIGN':0x48,
 'FUNCLABEL':0x4a,'GOTOLABEL':0x4b,'BRA_ENDIF':0x4c,'LINE':0x4d,
 'ALLOC_INT_ARRAY':0x4e,'ALLOC_FLOAT_ARRAY':0x4f,'ALLOC_ARRAY':0x50,
 'ALLOC_ARTICLE':0x51,'PRINT_ID':0x52,'PRINT_STRING':0x53,
}
BINOP = {'+':OP['ADD'],'-':OP['SUB'],'*':OP['MULTIPLY'],'/':OP['DIVIDE'],
         '%':OP['MOD'],'<':OP['LT'],'>':OP['GT'],'<=':OP['LE'],'>=':OP['GE'],
         '==':OP['EQ'],'!=':OP['NE'],'&':OP['AND'],'^':OP['XOR'],'|':OP['OR'],
         '&&':OP['LAND'],'||':OP['LOR']}
PREC = [['||'],['&&'],['|'],['^'],['&'],['==','!='],['<','>','<=','>='],
        ['+','-'],['*','/','%']]
TYPES = {'int':OP['INT'],'float':OP['FLOAT'],'string':OP['STRING'],
         'vector':OP['VECTOR'],'article':OP['ARTICLE']}
# words the exe treats as reserved: usable as statement/decl keywords but
# never as identifiers/member names/labels (probed against the exe)
RESERVED = {'to','step','int','float','string','vector','article','while',
            'for','next','break','continue','return','call','goto','shared',
            'global','do','if','else','elseif','endif','endwhile','switch',
            'case','default','endswitch','function','endfunction','array',
            'structure','endstructure'}
COMPOUND = {'+=':OP['PLUS_EQUALS'],'-=':OP['MINUS_EQUALS'],
            '*=':OP['MULT_EQUALS'],'/=':OP['DIV_EQUALS'],'%=':OP['MOD_EQUALS'],
            '&=':OP['AND_EQUALS'],'|=':OP['OR_EQUALS'],'^=':OP['XOR_EQUALS']}
BINOP_VALUES = frozenset(BINOP.values())


class Sym:
    __slots__ = ('name','id','shared','is_global','owner','claimed','is_func',
                 'ext','type','value')
    def __init__(self, name, sid):
        self.name = name
        self.id = sid
        self.shared = False
        self.is_global = False
        self.owner = -1
        self.claimed = False
        self.is_func = False
        self.ext = False
        self.type = 0
        self.value = 0


class Ins:
    __slots__ = ('op','args','slots','fn','tgt','extra')
    def __init__(self, op, args=()):
        self.op = op
        a = list(args)
        self.args = (a + [0]*(4-len(a)))[:4]
        self.slots = []
        self.fn = None
        self.tgt = None       # resolution info for jumps
        self.extra = None


class Ctx:
    def __init__(self):
        self.syms = []
        self.by_lower = {}
        self.strings = []
        self.str_id = {}
        self.ins = []
        self._all = []            # every Ins ever created (incl. cap buffers)
        self.funcs = []            # (funcSymId, firstInsIdx)
        self.fn_end = {}           # fid -> index of its closing RETURNVAL
        self.cur = None            # current function sym id
        self.fn_parent = {}        # fid -> enclosing function fid (None if top)
        self.ext_fnames = []
        for nm in BUILTINS:
            s = Sym(nm, len(self.syms)); self.syms.append(s)
            self.by_lower[nm.lower()] = s
        self.syms[0].type = 0x100
        self.syms[1].type = 0x100; self.syms[1].shared = True
        self.syms[2].type = 0x100; self.syms[2].shared = True
        self.syms[3].type = 2; self.syms[3].value = 1; self.syms[3].shared = True
        self.syms[4].type = 2; self.syms[4].value = 0; self.syms[4].shared = True
        self._pre_ident = None
        self._post_ident = None

    def ident(self, name, func=False, ext=False):
        if self._pre_ident is not None:
            self._pre_ident(self, name)
        s = self.by_lower.get(name.lower())
        if s is None:
            s = Sym(name, len(self.syms))
            self.syms.append(s)
            self.by_lower[name.lower()] = s
            if self._post_ident is not None:
                self._post_ident(self, s)
        if func: s.is_func = True
        if ext:
            s.ext = True
            s.type = 0x20
        return s.id

    def ext_mod(self, module):
        """Register an external module reference, keeping first-seen order."""
        if module.lower() not in [m.lower() for m in self.ext_fnames]:
            self.ext_fnames.append(module)

    def new_str(self, text):
        key = text.lower()
        if key in self.str_id:
            return self.str_id[key]
        self.str_id[key] = len(self.strings)
        self.strings.append(text)
        return self.str_id[key]

    def emit(self, op, args=()):
        ins = Ins(op, args)
        ins.fn = self.cur
        self.ins.append(ins)
        self._all.append(ins)
        return ins

    def sarg(self, ins, slot, sid):
        ins.slots.append(slot)
        ins.args[slot] = sid

    def sym(self, sid):
        return self.syms[sid]


class PErr(Exception):
    pass


def f32(v):
    return struct.unpack('<I', struct.pack('<f', v))[0]


class Parser:
    def __init__(self, c, text, src='<input>'):
        self.c = c
        self.src = src
        self.toks = tokenize(text)
        self.pos = 0
        self._adv()
        self.loops = []        # for break/continue targets
        self.labels = {}       # label lower-> Ins(GOTOLABEL)
        self.gotos = []        # (labelLower, Ins)
        # ---- _GetArticle deferred-pool machinery (see GA_RULE.md) ----
        self._deep = 0         # >0 inside ( ) / [ ] / call args
        self._ga = None        # Sym id of the _GetArticle func symbol
        self._ga_tail = False  # first member op = depth-0 read, nothing after yet
        self._tail_at = None   # len(c.syms) right after GA creation
        self._arm = None       # armed: swap GA past the NEXT id-headed head
        self._swap = False     # armed & current statement is id-headed
        self._pending = None   # GA id to swap once adjacent new head appears
        self._pstr = None      # (Ins,PUSHSTRING,fieldName): deferred member
                               # field-string registration
        self._pstr_defer = False
        c._pre_ident = self._pre_ident
        c._post_ident = self._post_ident

    def _defer_pstr(self, ins, text):
        """Defer the RTSN registration of a member field name used by a
        deferred (statement-final / condition / for-bound) member read."""
        ins.args[0] = -1
        self._pstr = (ins, text)

    def _resolve_pstr(self):
        """Register any deferred member field string now (dedupe as usual)."""
        if self._pstr is not None:
            ins, text = self._pstr
            self._pstr = None
            ins.args[0] = self.c.new_str(text)

    def _tail_reset(self):
        self._ga_tail = False
        self._tail_at = None

    def _arm_next(self):
        """Called at the top of parse_stmts for each real statement.  If a GA
        deferral is armed and this statement is identifier-headed, arm the swap
        that will fire at its head identifier; otherwise cancel the deferral
        (keyword-led statement -> oracle keeps GA at its natural spot)."""
        if self._arm is not None:
            if self.tok.kind == 'id' and self.tok.value.lower() not in RESERVED:
                self._swap = True
            else:
                self._arm = None

    def _pre_ident(self, c, name):
        """Runs before every symbol registration.  If a swap is armed and the
        head identifier of the armed statement is about to be appended right
        after _GetArticle, plan the pool swap; any other case cancels it."""
        if not self._swap:
            return
        self._swap = False
        self._arm = None
        self._pending = None
        if self._ga is not None and len(c.syms) == self._ga + 1 \
                and name.lower() != '_getarticle' \
                and c.by_lower.get(name.lower()) is None:
            self._pending = self._ga

    def _post_ident(self, c, s):
        """Runs after a NEW symbol is appended.  If it landed immediately
        after the deferred GA record, swap the two pool entries."""
        if self._pending is not None and s.id == self._pending + 1:
            self._ga_swap(self._pending, s.id)
        self._pending = None

    def _ga_swap(self, i, j):
        """Physically swap pool records i and j (i = _GetArticle, j = the new
        head identifier) and remap every id reference i<->j."""
        c = self.c
        a, b = c.syms[i], c.syms[j]
        c.syms[i], c.syms[j] = b, a
        a.id, b.id = j, i
        for ins in c._all:
            for slot in ins.slots:
                v = ins.args[slot]
                if v == i:
                    ins.args[slot] = j
                elif v == j:
                    ins.args[slot] = i
            ex = ins.extra
            if ex and ex[0] == 'fnoff' and ex[1] in (i, j):
                ins.extra = (ex[0], j if ex[1] == i else i)
        for k, (fid, idx) in enumerate(c.funcs):
            if fid == i:
                c.funcs[k] = (j, idx)
            elif fid == j:
                c.funcs[k] = (i, idx)
        c.by_lower[a.name.lower()] = a
        c.by_lower[b.name.lower()] = b
        if self._ga == i:
            self._ga = j
        self._pending = None

    def _adv(self):
        self.tok = self.toks[self.pos] if self.pos < len(self.toks) else self.toks[-1]
        return self.tok

    def _next(self):
        self.pos += 1
        return self._adv()

    def err(self, msg):
        raise PErr(f"{self.src}({self.tok.line}): {msg}")

    def kw(self, *ns):
        return self.tok.kind == 'id' and self.tok.value.lower() in ns

    def op(self, *ops):
        return self.tok.kind == 'op' and self.tok.value in ops

    def eat_kw(self, n):
        if self.kw(n):
            self._next(); return True
        return False

    def eat_op(self, o):
        if self.op(o):
            self._next(); return True
        return False

    def expect_op(self, o):
        if not self.eat_op(o):
            self.err(f"expected '{o}'")

    def expect_kw(self, n):
        if not self.eat_kw(n):
            self.err(f"expected '{n}'")

    def peek2(self):
        j = self.pos + 1
        return self.toks[j] if j < len(self.toks) else None

    # ========================================================== top level
    def parse_program(self):
        while self.tok.kind != 'eof':
            if self.kw('function'):
                self.parse_function()
            else:
                self.err("top-level statements unsupported")
        # the exe produces no output for a file with no functions
        if not self.c.funcs:
            self.err('no functions defined')
        return self.c

    def parse_function(self):
        """Parse a function definition.  Bodies are emitted inline, so a
        'function' keyword nested inside another body opens a nested
        function whose code lands right where it appeared (observed in
        qstloyalistbattle)."""
        self._next()                      # 'function'
        if self.tok.kind != 'id':
            self.err('function name')
        fname = self.tok.value
        self._next()
        fid = self.c.ident(fname, func=True)
        self.expect_op('(')
        params = []
        while not self.op(')'):
            if self.tok.kind != 'id':
                self.err('parameter name')
            params.append(self.c.ident(self.tok.value))
            self._next()
            if not self.eat_op(','):
                break
            if self.op(')'):
                self.err('trailing comma')   # function f(a,) rejected
        self.expect_op(')')
        # frame, emitted before the body
        start = len(self.c.ins)
        fl = self.c.emit(OP['FUNCLABEL'], [fid])
        fl.fn = fid
        fl.slots.append(0); fl.args[0] = fid
        for pid in reversed(params):
            ps = self.c.emit(OP['POPSYMBOL'])
            ps.fn = fid
            ps.slots.append(0); ps.args[0] = pid
        prev_cur = self.c.cur
        self.c.cur = fid
        self.parse_stmts(['endfunction'])
        self.expect_kw('endfunction')
        self.c.cur = prev_cur
        pn = self.c.emit(OP['PUSHSYMBOL'], [0])
        pn.fn = prev_cur
        pn.slots.append(0); pn.args[0] = 1     # None
        self.c.emit(OP['RETURNVAL'])
        self.c.funcs.append((fid, start))
        self.c.fn_end[fid] = len(self.c.ins) - 1
    def parse_stmts(self, terms):
        while True:
            if self.tok.kind == 'eof':
                self._resolve_pstr()
                self.err('expected ' + '/'.join(terms))
            # deferred member field-string flush cadence: a pending string
            # from a deferred read registers NOW unless the very next
            # statement is a non-identifier expression (literal/number/
            # parenthesized...): then it waits until after that statement
            if self._pstr is not None:
                t = self.tok
                nonid_expr = t.kind in ('num', 'str') or (
                    t.kind == 'op' and t.value in ('(', '-', '!'))
                if self._pstr_defer or not nonid_expr:
                    self._resolve_pstr()
                    self._pstr_defer = False
                else:
                    self._pstr_defer = True
            if self.kw(*terms):
                # block/function boundary: cancel any pending GA deferral
                self._arm = None
                return
            if self.op(';'):
                # empty statement; no opcode
                self._next()
                continue
            # fresh statement: reset tail flags, maybe arm GA swap for an
            # id-headed statement
            self._tail_reset()
            self._arm_next()
            if self.kw('function'):
                # nested function definition inline (observed corpus)
                self.parse_function()
            elif self.kw('if'):
                self.parse_if()
            elif self.kw('while'):
                self.parse_while()
            elif self.kw('for'):
                self.parse_for()
            elif self.kw('return'):
                self.parse_return()
            elif self.kw('shared', 'global'):
                self.parse_shared()
            elif self.kw('break'):
                self._next(); ins = self.c.emit(OP['BREAK']); self.eat_op(';')
                self._reg_break(ins)
            elif self.kw('continue'):
                self._next(); ins = self.c.emit(OP['CONTINUE']); self.eat_op(';')
                self._reg_break(ins)
            elif self.kw('switch'):
                self._next()
                # the selector is an assignment-expression (the exe accepts
                # e.g. a stray 'switch x = 1' after an unclosed switch)
                self.parse_assign_expr()
                self.c.emit(OP['SWITCH'])
            elif self.kw('case'):
                self._next()
                # 'case 1,2' pushes every label value, then one CASE
                self.parse_assign_expr()
                while self.op(','):
                    self._next()
                    self.parse_assign_expr()
                self.c.emit(OP['CASE'], [0])
                self.eat_op(';')
            elif self.kw('goto'):
                self._next()
                lbl = self.tok.value if self.tok.kind == 'id' else self.err('label')
                if lbl.lower() in RESERVED:
                    self.err(f"'{lbl}' is a reserved word")
                self._next()
                sid = self.c.ident(lbl)
                g = self.c.emit(OP['GOTO'], [0])
                g.extra = lbl.lower()
                self.gotos.append((lbl.lower(), g))
                self.eat_op(';')
            elif self.kw('call'):
                self._next()
                self.parse_call(kw_call=True)
            elif self.tok.kind == 'id' and self.peek2() is not None \
                    and self.peek2().kind == 'op' and self.peek2().value == ':':
                # label definition (GOTOLABEL); decoration only happens when a
                # goto actually targets the label (see decorate)
                lbl = self.tok.value
                if lbl.lower() in RESERVED:
                    self.err(f"'{lbl}' is a reserved word")
                if lbl.lower() in self.labels:
                    self.err(f"label '{lbl}' already defined")
                self._next(); self._next()
                sid = self.c.ident(lbl)
                lab = self.c.emit(OP['GOTOLABEL'], [0])
                lab.slots.append(0); lab.args[0] = sid
                self.labels[lbl.lower()] = lab
            elif self.tok.kind == 'id' and self.tok.value.lower() in TYPES:
                self.parse_typed_decl()
            elif self.kw('array'):
                self.parse_typed_decl()
            else:
                self.parse_expr_statement()

    def _reg_break(self, ins):
        pass   # patched later with loop scope info

    def parse_return(self):
        self._next()
        if self.eat_op(';'):
            self._return_none()
        elif self.op('('):
            self._next()
            if self.eat_op(')'):
                self._return_none()
            else:
                self.parse_assign_expr()
                self.expect_op(')')
                self.c.emit(OP['RETURNVAL'])
        else:
            self.err('return needs (expr) or ;')
        self.eat_op(';')

    def _return_none(self):
        # return with no value pushes None (shared) then RETURNVAL
        ps = self.c.emit(OP['PUSHSYMBOL']); self.c.sarg(ps, 0, 1)
        self.c.emit(OP['RETURNVAL'])

    def parse_shared(self):
        is_shared_kw = self.kw('shared')
        self._next()                      # 'shared' or 'global'
        typ = None
        if self.tok.kind == 'id' and (self.tok.value.lower() in TYPES
                                      or self.tok.value.lower() == 'array'):
            typ = self.tok.value.lower()
            self._next()
        name = self.tok.value if self.tok.kind == 'id' else self.err('var name')
        self._next()
        sid = self.c.ident(name)
        if self.op('['):
            # array declaration
            alloc = self._array_alloc(typ)
            if alloc is None:
                self.err(f"array of type '{typ}' not supported")
            self._next()
            saved = self._cap()
            self.parse_expr(0)
            size_ins = self._endcap(saved)
            self.expect_op(']')
            if is_shared_kw:
                self.c.sym(sid).shared = True
            else:
                self.c.sym(sid).is_global = True
            ps = self.c.emit(OP['PUSHSYMBOL']); self.c.sarg(ps, 0, sid)
            for ins in size_ins:
                self.c.ins.append(ins)
            self.c.emit(alloc)
            self.eat_op(';')
            return
        if self.eat_op('='):
            if is_shared_kw:
                self.c.sym(sid).shared = True
            else:
                self.c.sym(sid).is_global = True
            if typ is not None:
                t = self.c.emit(TYPES[typ]); self.c.sarg(t, 0, sid)
            ps = self.c.emit(OP['PUSHSYMBOL']); self.c.sarg(ps, 0, sid)
            self.parse_expr(0)
            self.c.emit(OP['ASSIGN'])
            self.c.emit(OP['DISCARD'])
            self.eat_op(';')
            return
        # 'shared X' without '=' behaves like a typed local decl (observed):
        # typed no-init stays a local; untyped no-init still marks shared.
        if typ is not None:
            t = self.c.emit(TYPES[typ]); self.c.sarg(t, 0, sid)
        else:
            if is_shared_kw:
                self.c.sym(sid).shared = True
            else:
                self.c.sym(sid).is_global = True
        self.eat_op(';')

    def parse_assign_lvalue(self):
        name = self.tok.value
        self._next()
        sid = self.c.ident(name)
        # base symbol is pushed before any index expression
        if self.op('['):
            ps = self.c.emit(OP['PUSHSYMBOL']); self.c.sarg(ps, 0, sid)
            while self.op('['):
                self._next()
                self.parse_expr(0)
                self.expect_op(']')
                self.c.emit(OP['ARRAYREF'])
            # value pushed via ARRAYREF; plain symbol lvalue: nothing yet
        else:
            ps = self.c.emit(OP['PUSHSYMBOL']); self.c.sarg(ps, 0, sid)
        if self.eat_op('='):
            self.parse_expr(0)
            self.c.emit(OP['ASSIGN'])
        elif self.tok.kind == 'op' and self.tok.value in COMPOUND:
            op = COMPOUND[self.tok.value]
            self._next()
            self.parse_expr(0)
            self.c.emit(op)
        else:
            self.err('expected = or compound assignment')
        self.c.emit(OP['DISCARD'])
        self.eat_op(';')

    def parse_plain_assign(self):
        name = self.tok.value
        self._next(); self.expect_op('=')
        sid = self.c.ident(name)
        ps = self.c.emit(OP['PUSHSYMBOL']); self.c.sarg(ps, 0, sid)
        self.parse_expr(0)
        self.c.emit(OP['ASSIGN'])
        self.c.emit(OP['DISCARD'])
        self.eat_op(';')

    def _array_alloc(self, typ):
        """Element alloc opcode for a [size] array decl.  Only int, float and
        the bare 'array' type are legal (observed in the exe); string/vector/
        article arrays are rejected at parse time."""
        return {'int': OP['ALLOC_INT_ARRAY'], 'float': OP['ALLOC_FLOAT_ARRAY'],
                'array': OP['ALLOC_ARRAY']}.get(typ)

    def parse_typed_decl(self):
        typ = self.tok.value.lower()
        self._next()
        name = self.tok.value if self.tok.kind == 'id' else self.err('var name')
        self._next()
        sid = self.c.ident(name)
        if self.op('['):
            # array declaration: no initializer allowed, no type node emitted
            alloc = self._array_alloc(typ)
            if alloc is None:
                self.err(f"array of type '{typ}' not supported")
            self._next()
            saved = self._cap()
            self.parse_expr(0)
            size_ins = self._endcap(saved)
            self.expect_op(']')
            ps = self.c.emit(OP['PUSHSYMBOL']); self.c.sarg(ps, 0, sid)
            for ins in size_ins:
                self.c.ins.append(ins)
            self.c.emit(alloc)
            self.eat_op(';')
            return
        if typ == 'array':
            self.err('array needs a size: array name[size]')
        if typ == 'vector' and (self.op('=') or self.tok.kind == 'op'
                                and self.tok.value in COMPOUND):
            self.err('vector declarations may not have an initializer')
        t = self.c.emit(TYPES[typ]); self.c.sarg(t, 0, sid)
        if self.eat_op('='):
            ps = self.c.emit(OP['PUSHSYMBOL']); self.c.sarg(ps, 0, sid)
            self.parse_expr(0)
            self.c.emit(OP['ASSIGN'])
            self.c.emit(OP['DISCARD'])
        self.eat_op(';')

    def parse_assign_expr(self):
        """Parse one assignment-expression (right-assoc '='/'op='), recursing
        into the RHS so chains like a = b = 1 work."""
        self.parse_expr(0)
        if self.tok.kind == 'op' and self.tok.value == '=':
            if self.c.ins and self.c.ins[-1].op in BINOP_VALUES:
                self.err('cannot assign to expression')   # a + b = 1
            self._tail_reset()            # writing to a member LHS: no deferral
            self._resolve_pstr()
            self._next()
            self.parse_assign_expr()
            self.c.emit(OP['ASSIGN'])
        elif self.tok.kind == 'op' and self.tok.value in COMPOUND:
            if self.c.ins and self.c.ins[-1].op in BINOP_VALUES:
                self.err('cannot assign to expression')
            self._tail_reset()
            self._resolve_pstr()
            op = COMPOUND[self.tok.value]
            self._next()
            self.parse_assign_expr()
            self.c.emit(op)

    def parse_expr_statement(self):
        # an expression statement = comma list of assignment expressions
        #   a = 1, b = 2   (single trailing DISCARD)
        self._tail_reset()
        self.parse_assign_expr()
        while self.op(','):
            self._tail_reset()            # continuation: no tail-read deferral
            self._resolve_pstr()
            self._next()
            self.parse_assign_expr()
        self.c.emit(OP['DISCARD'])
        self.eat_op(';')
        # if this statement ended with the first member op being a depth-0
        # READ (nothing record-producing followed), the oracle defers its
        # _GetArticle pool record to right after the NEXT id-headed
        # statement's head identifier (see _arm_next)
        if self._ga_tail and self._tail_at == len(self.c.syms):
            self._arm = self._ga
        # a pending deferred member field string (this statement ended in a
        # member-data read) is left armed: parse_stmts cadence resolves it
        self._tail_reset()

    def parse_cond(self):
        # conditions parse as assignment-expressions (a bare 'x = y' or
        # '(a) == (b)' are valid); leading parens are only expression
        # grouping, not a mandatory condition wrapper
        self.parse_assign_expr()

    def _arm_cond_tail(self):
        """A depth-0 READ that ended a bare if/while/elseif condition gets the
        same _GetArticle pool deferral as a statement-final read: it is armed
        for the first id-headed statement of the branch body."""
        if self._ga_tail and self._tail_at == len(self.c.syms):
            self._arm = self._ga
        # a pending deferred member field string is left armed: parse_stmts
        # cadence resolves it according to the branch body's first statement
        self._tail_reset()

    def _require_branch_body(self, kws):
        """The exe rejects elseif/else branches whose body contains no
        statement; raise PErr if only a branch terminator follows."""
        j = self.pos
        while j < len(self.toks) and self.toks[j].kind == 'op' \
                and self.toks[j].value == ';':
            j += 1
        t = self.toks[j]
        if t.kind == 'eof' or (t.kind == 'id' and t.value.lower() in kws):
            self.err('branch body may not be empty')

    def parse_if(self):
        self._next()                      # 'if'
        self.parse_cond()
        self._arm_cond_tail()
        iff = self.c.emit(OP['IF'], [0])
        # then-branch statements
        self._parse_until_branch()
        # does a following branch exist?
        branches = []                     # list of head-ins (IF/ELSEIF)
        branch_bra = {}                   # headIns -> trailing BRA ins or None
        head = iff
        while True:
            nxt = self.tok.value.lower() if self.tok.kind == 'id' else ''
            if nxt in ('elseif', 'else', 'endif'):
                more = nxt in ('elseif', 'else')
                bra = None
                if more:
                    bra = self.c.emit(OP['BRA_ENDIF'], [0])
                branch_bra[head] = bra
                if not more:
                    break
                if nxt == 'elseif':
                    self._next()
                    self.parse_cond()
                    self._arm_cond_tail()
                    head = self.c.emit(OP['ELSEIF'], [0])
                    branches.append(head)
                    self._require_branch_body(
                        ('elseif', 'else', 'endif', 'endfunction'))
                    self._parse_until_branch()
                    continue
                else:  # 'else'
                    self._next()
                    self._require_branch_body(('endif', 'endfunction'))
                    self.parse_stmts(['endif'])
                    self.c.emit(OP['ELSE'])
                    break
            else:
                # shouldn't happen (parse_until_branch stops at those)
                break
        endif = self.c.emit(OP['ENDIF'])
        self.expect_kw('endif')
        # record targets: head target = first instr after its body's BRA, else ENDIF
        for h in [iff] + branches:
            bra = branch_bra.get(h)
            h.tgt = ('after', bra) if bra is not None else ('ins', endif)
        # BRA_ENDIF instructions target ENDIF
        for bra in branch_bra.values():
            if bra is not None:
                bra.tgt = ('ins', endif)

    def _parse_until_branch(self):
        self.parse_stmts(['elseif', 'else', 'endif'])

    def parse_while(self):
        self._next()
        wh = self.c.emit(OP['WHILE'])
        self.parse_cond()
        self._arm_cond_tail()
        go = self.c.emit(OP['GOENDWHILE'], [0])
        self.loops.append({'go': go, 'wh': wh})
        self.parse_stmts(['endwhile'])
        ew = self.c.emit(OP['ENDWHILE'], [0])
        self.loops.pop()
        self.expect_kw('endwhile')
        wh.tgt = None              # WHILE emits no operand
        go.tgt = ('exit', ew)
        ew.tgt = ('back', wh)

    def _cap(self):
        saved = self.c.ins
        self.c.ins = []
        return saved

    def _endcap(self, saved):
        tmp = self.c.ins
        self.c.ins = saved
        return tmp

    def parse_for(self):
        self._next()          # 'for'
        if self.tok.kind != 'id':
            self.err('loop variable')
        vname = self.tok.value
        var = self.c.ident(vname)     # register in token order
        self._next()
        self.expect_op('=')
        saved = self._cap()
        self.parse_expr(0)
        start_ins = self._endcap(saved)
        self._tail_reset()             # start-expr read flushes naturally
        self._resolve_pstr()
        self.expect_kw('to')
        saved = self._cap()
        self.parse_expr(0)
        end_ins = self._endcap(saved)
        step_ins = None
        if self.kw('step'):
            # a 'step' clause makes the end-expr read flush naturally (its
            # field string registers before the step strings)
            self._tail_reset()
            self._next()
            self._resolve_pstr()
            saved = self._cap()
            self.parse_expr(0)
            step_ins = self._endcap(saved)
            self._arm_cond_tail()      # step-expr read defers past body head
        else:
            # no step: end-expr read defers past the body's first statement
            self._arm_cond_tail()
        # loop variable is typed by the "start" expression's guessed type
        typ = self._start_type(start_ins)
        is_float = typ == OP['FLOAT']
        if typ is not None:
            t = self.c.emit(typ); self.c.sarg(t, 0, var)
        ps = self.c.emit(OP['PUSHSYMBOL']); self.c.sarg(ps, 0, var)
        for ins in start_ins:
            self.c.ins.append(ins)
        self.c.emit(OP['ASSIGN'])
        self.c.emit(OP['DISCARD'])
        forop = self.c.emit(OP['FOR'])
        body_start = len(self.c.ins)
        # parse body until 'next'
        self.parse_stmts(['next'])
        self._next()          # 'next'
        if self.tok.kind == 'id':
            nxtname = self.tok.value
            if nxtname.lower() != vname.lower():
                self.err(f"next {nxtname} does not match for {vname}")
            self._next()
        # loop condition: var < end
        ps = self.c.emit(OP['PUSHSYMBOL']); self.c.sarg(ps, 0, var)
        for ins in end_ins:
            self.c.ins.append(ins)
        self.c.emit(OP['LT'])
        skip = self.c.emit(OP['SKIP'], [0])
        # increment var += step (default 1)
        ps = self.c.emit(OP['PUSHSYMBOL']); self.c.sarg(ps, 0, var)
        if step_ins is None:
            if is_float:
                self.c.emit(OP['PUSHFLOAT'], [f32(1.0)])
            else:
                self.c.emit(OP['PUSHINT'], [1])
        else:
            for ins in step_ins:
                self.c.ins.append(ins)
        self.c.emit(OP['PLUS_EQUALS'])
        self.c.emit(OP['DISCARD'])
        nxt = self.c.emit(OP['NEXT_SYM'], [0])
        self.eat_op(';')
        # jumps: NEXT_SYM loops back to instruction right after FOR;
        # SKIP jumps past NEXT_SYM (loop exit)
        nxt.tgt = ('after', forop)
        skip.tgt = ('after', nxt)

    def _start_type(self, ins):
        """BestGuessType of the for-loop start expression (oracle): scan the
        emitted instructions in code order and return the type of the FIRST
        push with a definite type -- PUSHINT->INT, PUSHFLOAT->FLOAT,
        PUSHSTRING->STRING (a member data read starts with its PUSHSTRING, so
        it reads STRING).  Plain symbols, calls/method calls with no literal
        arguments and arrayrefs of symbols guess nothing -> no typed decl.
        (The exe scans emitted pushes, so literal call arguments count.)"""
        if not ins:
            return OP['INT']
        for i in ins:
            if i.op == OP['PUSHINT']:
                return OP['INT']
            if i.op == OP['PUSHFLOAT']:
                return OP['FLOAT']
            if i.op == OP['PUSHSTRING']:
                return OP['STRING']
        return None

    def _expr_is_float(self, ins):
        if not ins:
            return False
        # start instruction PUSHFLOAT indicates float; symbols default int
        for i in ins:
            if i.op == OP['PUSHFLOAT']:
                return True
            if i.op == OP['PUSHINT']:
                return False
        return False



    def parse_call(self, kw_call=False):
        if self.tok.kind != 'id':
            self.err('call target')
        name = self.tok.value
        self._next()
        module = None
        if self.eat_op('@'):
            module = self.tok.value if self.tok.kind == 'id' else self.err('module')
            self._next()
        full = name if module is None else f"{name}@{module}"
        sid = self.c.ident(full, func=True, ext=(module is not None))
        if module:
            self.c.ext_mod(module)
        n = self.parse_args()
        if module is not None:
            ins = self.c.emit(OP['CALL'], [2, sid, n])
            ins.slots.append(1); ins.args[1] = sid
        else:
            # local function call: operand = offset of its FUNCLABEL instr
            ins = self.c.emit(OP['CALL'], [1, 0, n])
            ins.extra = ('fnoff', sid)
        if kw_call:
            self.c.emit(OP['DISCARD'])
            self.eat_op(';')

    def parse_args(self):
        self.expect_op('(')
        self._deep += 1
        n = 0
        while not self.op(')'):
            saved = self._cap()
            self.parse_assign_expr()
            tmp = self._endcap(saved)
            for ins in tmp:
                self.c.ins.append(ins)
            n += 1
            # an argument that is itself a module `call` counts its own argc
            if tmp and tmp[-1].op == OP['CALL'] and len(tmp[-1].args) > 2:
                n += tmp[-1].args[2]
            if not self.eat_op(','):
                break
            if self.op(')'):
                self.err('trailing comma')   # g(a,) rejected
        self.expect_op(')')
        self._deep -= 1
        return n

    # ======================================================== expressions
    def parse_expr(self, minp=0):
        t = self.tok
        if t.kind == 'num':
            self._next()
            raw = t.value
            is_hex = len(raw) > 1 and raw[:2].lower() == '0x'
            if not is_hex and ('.' in raw or 'e' in raw.lower()):
                self.c.emit(OP['PUSHFLOAT'], [f32(float(raw))])
            else:
                v = int(raw, 0)
                # decimal ints saturate at 0x7FFFFFFF (observed); hex stays raw
                if not (len(raw) > 1 and raw[:2].lower() == '0x') \
                        and v > 0x7FFFFFFF:
                    v = 0x7FFFFFFF
                self.c.emit(OP['PUSHINT'], [v])
            self._idx_postfix()
        elif t.kind == 'str':
            self._next()
            self.c.emit(OP['PUSHSTRING'], [self.c.new_str(dec_esc(t.value))])
            self._idx_postfix()
        elif t.kind == 'id':
            low = t.value.lower()
            if low in ('true', 'false', 'null', 'none', 'self'):
                self._next()
                sid = self.c.ident(t.value)
                self.parse_postfix(sid)
            elif low == 'call':
                self._next()
                name = self.tok.value if self.tok.kind == 'id' else self.err('fn')
                self._next()
                module = None
                if self.eat_op('@'):
                    module = self.tok.value if self.tok.kind == 'id' else self.err('module')
                    self._next()
                full = name if module is None else f"{name}@{module}"
                sid = self.c.ident(full, func=True, ext=(module is not None))
                if module:
                    self.c.ext_mod(module)
                n = self.parse_args()
                if module is not None:
                    ins = self.c.emit(OP['CALL'], [2, sid, n])
                    ins.slots.append(1); ins.args[1] = sid
                else:
                    ins = self.c.emit(OP['CALL'], [1, 0, n])
                    ins.extra = ('fnoff', sid)
                self._idx_postfix()
            else:
                self._next()
                self.idref(t.value)
        elif t.kind == 'op' and t.value == '-':
            self._next(); self.parse_expr(9); self.c.emit(OP['UNEG'])
        elif t.kind == 'op' and t.value == '!':
            self._next(); self.parse_expr(9); self.c.emit(OP['LNEG'])
        elif t.kind == 'op' and t.value == '(':
            # parenthesized expression, possibly a comma list: each
            # sub-expression is evaluated in order and left on the stack
            # (used for vector literals and multi-value assignments);
            # elements may be assignment-expressions
            self._next()
            self._deep += 1
            self.parse_assign_expr()
            while self.op(','):
                self._next()
                self.parse_assign_expr()
            self.expect_op(')')
            self._deep -= 1
            self._idx_postfix()          # (a)[0], (5)[0], (g())[0] allowed
        else:
            self.err(f"bad expression '{t.value}'")
        # infix
        while True:
            t = self.tok
            op = None
            if t.kind == 'op' and t.value in BINOP:
                op = t.value
            if op is None:
                return
            pr = self._prec(op)
            if pr < minp:
                return
            self._next()
            self._tail_reset()          # binop: read is not statement-final
            self._resolve_pstr()
            self.parse_expr(pr + 1)
            self.c.emit(BINOP[op])

    def _prec(self, op):
        for i, lvl in enumerate(PREC):
            if op in lvl:
                return i
        return -1

    def idref(self, name):
        if name.lower() in RESERVED:
            self.err(f"'{name}' is a reserved word")
        if self.op('@'):
            self._next()
            module = self.tok.value if self.tok.kind == 'id' else self.err('module')
            self._next()
            full = f"{name}@{module}"
            sid = self.c.ident(full, func=True, ext=True)
            self.c.ext_mod(module)
            n = self.parse_args()
            ins = self.c.emit(OP['CALLFUNC'], [sid, n, 0x40000000])
            ins.slots.append(0); ins.args[0] = sid
            self._idx_postfix()
            return
        sid = self.c.ident(name)
        self.parse_postfix(sid)

    def _idx_postfix(self):
        """'[...]' chains after a value already left on the stack (literal,
        call/module-call result): each index pops the base and pushes the
        element via ARRAYREF."""
        while self.op('['):
            self._next()
            self._deep += 1
            self.parse_expr(0)
            self._deep -= 1
            self.expect_op(']')
            self.c.emit(OP['ARRAYREF'])

    def parse_postfix(self, sid):
        """Emit a value/lvalue expression for symbol sid plus postfix chain."""
        while True:
            if self.op('('):
                self.c.sym(sid).is_func = True
                n = self.parse_args()
                ins = self.c.emit(OP['CALLFUNC'], [sid, n, 0x40000000])
                ins.slots.append(0); ins.args[0] = sid
                # call result is on the stack; only indexing may follow,
                # member access after a call is rejected by the exe
                sid = None
                if self.op('['):
                    continue
                return
            elif self.op('.'):
                self._next()
                m = self.tok.value if self.tok.kind == 'id' else self.err('member')
                if m.lower() in RESERVED:
                    self.err(f"'{m}' is a reserved word")
                self._next()
                if self.op('('):
                    # member call on object sid
                    msid = self.c.ident(m, func=True)
                    n = self.parse_args()
                    ins = self.c.emit(OP['CALLFUNC'], [msid, n, sid])
                    ins.slots.append(0); ins.args[0] = msid
                    ins.slots.append(2); ins.args[2] = sid
                    sid = None       # value is now on stack; no more '.'-chains
                    if self.op('['):
                        continue
                    return
                else:
                    # member data read: _GetArticle with the member NAME as a
                    # string built from the resolved symbol's canonical name
                    msid = self.c.ident(m)
                    ga_new = self._ga is None \
                        and self.c.by_lower.get('_getarticle') is None
                    aid = self.c.ident('_GetArticle', func=True)
                    if ga_new:
                        self._ga = aid
                        # a depth-0 READ is a deferral candidate: oracle moves
                        # its pool slot after the next id-headed statement head
                        if self._deep == 0:
                            self._ga_tail = True
                            self._tail_at = len(self.c.syms)
                    pstr = self.c.emit(OP['PUSHSTRING'], [0])
                    if self._deep == 0 and self._pstr is None:
                        # defer the field-name RTSN registration: it lands
                        # with the deferred GA record flush cadence
                        self._defer_pstr(pstr, self.c.sym(msid).name)
                    else:
                        pstr.args[0] = self.c.new_str(self.c.sym(msid).name)
                    ins = self.c.emit(OP['CALLFUNC'], [aid, 1, sid])
                    ins.slots.append(0); ins.args[0] = aid
                    ins.slots.append(2); ins.args[2] = sid
                    sid = None
                    if self.op('['):
                        continue
                    return
            elif self.op('['):
                # first index needs the base symbol pushed
                if sid is not None:
                    ps = self.c.emit(OP['PUSHSYMBOL']); self.c.sarg(ps, 0, sid)
                    sid = None
                # a member read continuing into an index is not a deferral
                # candidate; reads inside the bracket parse at depth>0
                self._resolve_pstr()
                self._next()
                self._deep += 1
                self.parse_expr(0)
                self._deep -= 1
                self.expect_op(']')
                self.c.emit(OP['ARRAYREF'])
                continue
            else:
                # bare symbol value (already pushed for member/index results)
                if sid is not None:
                    ps = self.c.emit(OP['PUSHSYMBOL']); self.c.sarg(ps, 0, sid)
                return


def dec_esc(raw):
    """Decode escapes + strip gender-code prefixes from a string literal body.
    Matches Compiler::FixupStringControlChars: \\n \\t \\" \\' \\\\ plus
    '##CC' gender markers (optionally '##CC<name>') are removed.
    """
    out = []
    i = 0
    n = len(raw)
    mp = {'n': '\n', 't': '\t', '"': '"', "'": "'", '\\': '\\'}
    while i < n:
        c = raw[i]
        if c == '\\' and i + 1 < n and raw[i+1] in mp:
            out.append(mp[raw[i+1]])
            i += 2
        elif c == '#' and i + 1 < n and raw[i+1] == '#':
            # gender-code prefix '##CC' (two chars); nothing after it is removed
            i += 4
        else:
            out.append(c)
            i += 1
    return ''.join(out)


# =============================================================== decoration
def _base(name):
    return name.rsplit('#', 1)[0]

VAR_OPS = (OP['PUSHSYMBOL'], OP['POPSYMBOL'], OP['INT'], OP['FLOAT'],
           OP['STRING'], OP['VECTOR'], OP['ARTICLE'])


def decorate(c):
    """Mimic Dog::Compiler::ProcessAllFunctions/ProcessFunctionDefinition."""
    # functions were appended in source order (nested defs complete before
    # their enclosing function); processing order is reversed.  A function's
    # decoration region is its FULL inline span, which includes any nested
    # function bodies emitted inside it: the outer function claims an inner
    # function's local in place first, and the inner function later receives
    # an end-appended name#fid copy (observed phantom records).
    funcs = list(c.funcs)
    defined = {fid for fid, _ in funcs}
    for fid, start in reversed(funcs):
        end = c.fn_end.get(fid, len(c.ins) - 1)
        # collect variable symbol references of this function in order
        refs = []          # (ins, slot, sid)
        for idx in range(start, end + 1):
            ins = c.ins[idx]
            for slot in ins.slots:
                sid = ins.args[slot]
                s = c.syms[sid]
                # function/ext symbols are never decorated as such; a variable
                # use whose name collides with a method/call symbol gets a
                # per-function local copy (name#fid) instead - EXCEPT a
                # locally defined function used as a value keeps its symbol.
                # (A builtin like `self` that is *called* (self(x)) becomes
                # function-like: later VALUE uses copy it per function.)
                if s.is_func or s.ext:
                    if ins.op in VAR_OPS and sid not in defined:
                        refs.append((ins, slot, sid))
                    elif ins.op == OP['CALLFUNC'] and slot == 2 and sid != fid:
                        # member-operation object operand: a function-like
                        # symbol used as an object gets a per-function copy
                        # (unless it is this function's own name)
                        refs.append((ins, slot, sid))
                    continue
                # builtins (self,None,Null,True,False) & shared/global
                # file-scope vars are never decorated
                if s.id < 5 or s.shared or s.is_global:
                    continue
                # GOTOLABEL markers themselves do not decorate the label; only
                # an actual goto targeting it does (see below)
                if ins.op == OP['GOTOLABEL']:
                    continue
                refs.append((ins, slot, sid))
            if ins.op == OP['GOTO'] and ins.extra:
                s = c.by_lower.get(ins.extra)
                if s is not None and s.id >= 5 and not s.shared and not s.is_global \
                        and not s.is_func and not s.ext:
                    refs.append((ins, None, s.id))
        for ins, slot, sid in refs:
            s = c.syms[sid]
            if (s.is_func or s.ext) or (s.owner >= 0 and s.owner != fid):
                # copy path: the base record can never be this function's
                # local (it is a function/ext symbol, or already claimed by
                # another function); append/reuse name#fid instead
                base = _base(s.name)
                newname = f"{base}#{fid}"
                # search for existing symbol with this decorated name
                nsid = None
                for other in c.syms:
                    if other.name.lower() == newname.lower():
                        nsid = other.id
                        break
                if nsid is None:
                    ns = Sym(newname, len(c.syms))
                    ns.owner = fid
                    ns.claimed = True
                    c.syms.append(ns)
                    c.by_lower[newname.lower()] = ns
                    nsid = ns.id
                if slot is not None:
                    ins.args[slot] = nsid
            else:
                # first claim: rename in place with this function's id
                s.name = f"{_base(s.name)}#{fid}"
                s.owner = fid
                s.claimed = True


def _shared_ordinals(c):
    ords = {}
    n = 0
    for s in c.syms:
        if s.shared:
            ords[s.id] = n
            n += 1
    return ords


def _patch_and_serialize(c):
    """Compute per-instruction offsets, patch jumps & shared encodings,
    and produce the EDOC opcode bytes."""
    LEN = OC.LEN
    # first rewrite shared symbol operands & resolve goto label ids
    ords = _shared_ordinals(c)
    for ins in c.ins:
        if ins.op in (OP['GOTO'],):
            # operand = label symbol id (resolved by name)
            if ins.extra is not None:
                s = c.by_lower.get(ins.extra)
                if s is not None:
                    ins.args[0] = s.id
        for slot in list(ins.slots):
            sid = ins.args[slot]
            s = c.syms[sid]
            if s.shared:
                ins.args[slot] = SHARED_BIT | ords[sid]
    # PUSHSTRING operands are byte offsets inside the RTSN area
    stroff = {}
    off = 0
    for idx, text in enumerate(c.strings):
        stroff[idx] = off
        off += len(text.encode('cp1252')) + 1
    # RTSS area offsets (symbol names) used by local-function 'call' operands
    symoff = {}
    off = 0
    for s in c.syms:
        symoff[s.id] = off
        off += len(s.name.encode('cp1252')) + 1
    for ins in c.ins:
        if ins.op == OP['PUSHSTRING']:
            ins.args[0] = stroff[ins.args[0]]
    # instruction offsets
    off = {}
    pos = 0
    for ins in c.ins:
        off[ins] = pos
        pos += LEN.get(ins.op, 1)
    # local function calls: operand = byte offset of the callee FUNCLABEL.
    # A 'call name()' whose name is not defined locally falls back to a
    # name-based external call: CALL [2, funcSymId, argc]
    fn_start = {fid: c.ins[first] for fid, first in c.funcs}
    for ins in c.ins:
        if ins.extra and ins.extra[0] == 'fnoff':
            sid = ins.extra[1]
            tgt = fn_start.get(sid)
            if tgt is not None:
                ins.args[1] = off[tgt]
            else:
                ins.args[0] = 2
                ins.args[1] = sid
    # patch jump targets
    def off_of(ref):
        return off[ref]

    for ins in c.ins:
        if ins.tgt is None:
            continue
        kind = ins.tgt[0]
        if kind == 'ins':
            ins.args[0] = off[ins.tgt[1]]
        elif kind == 'after':
            bra = ins.tgt[1]
            ins.args[0] = off[bra] + LEN.get(bra.op, 1)
        elif kind == 'while':
            _, go, ew = ins.tgt
            ins.args[0] = off[wh] if False else 0     # WHILE has no operand
        elif kind == 'exit':
            ew = ins.tgt[1]
            ins.args[0] = off[ew] + LEN.get(ew.op, 1)
        elif kind == 'back':
            wh = ins.tgt[1]
            ins.args[0] = off[wh] + LEN.get(wh.op, 1)
    # serialize
    out = bytearray()
    for ins in c.ins:
        ln = LEN.get(ins.op)
        if ln is None or OC.FLAG.get(ins.op, 0) == 0:
            # structural opcode not serialized
            continue
        out.append(ins.op)
        ndw = (ln - 1) // 4
        for k in range(ndw):
            out += struct.pack('<I', ins.args[k] & 0xFFFFFFFF)
    return bytes(out), off


def _pools(c):
    # RTSS names in symbol order
    names = [s.name for s in c.syms]
    offsets = []
    pos = 0
    for n in names:
        offsets.append(pos)
        pos += len(n.encode('cp1252')) + 1
    bmys = bytearray()
    for s in c.syms:
        bmys += struct.pack('<III', offsets[s.id], s.type, s.value)
    ysas = bytearray()
    for s in c.syms:
        if s.shared:
            ysas += struct.pack('<III', offsets[s.id], s.type, s.value)
    rtsn_area = b''.join(t.encode('cp1252') + b'\x00' for t in c.strings)
    return names, offsets, bytes(bmys), bytes(ysas), rtsn_area


def _crc0(b):
    c = 0
    for x in b:
        c ^= x
        for _ in range(8):
            c = (c >> 1) ^ 0xEDB88320 if c & 1 else c >> 1
    return c


def _elif_order(mods):
    """Order ELIF names the way the original writer does: CRC32 (init 0,
    no final XOR) over the uppercase-folded 'name.scp', 32 buckets, and
    newest-first within each bucket."""
    seen = {}
    for i, m in enumerate(mods):
        seen.setdefault(m.lower(), i)
    keyed = []
    for m in mods:
        name = m + '.scp'
        folded = ''.join(chr(ord(ch) & 0xDF) for ch in name)
        h = _crc0(folded.encode('cp1252')) & 31
        keyed.append((h, -seen[m.lower()], m + '.scp'))
    return [x[2] for x in sorted(keyed)]


def compile_source(text, src='<input>'):
    c = Ctx()
    Parser(c, text, src).parse_program()
    decorate(c)
    code, off = _patch_and_serialize(c)
    names, offsets, bmys, ysas, _rtsn_area = _pools(c)
    # TRPX export records: one per FUNCLABEL and per GOTOLABEL instruction,
    # in code order: [0,0,rtssOffset,nameCodeOffset,0..]
    trpx = bytearray()
    n_exp = 0
    for ins in c.ins:
        if ins.op not in (OP['FUNCLABEL'], OP['GOTOLABEL']):
            continue
        rec = bytearray(36)
        sid = ins.args[0]
        struct.pack_into('<I', rec, 8, offsets[sid])
        struct.pack_into('<I', rec, 12, off.get(ins, 0))
        trpx += rec
        n_exp += 1
    elif_names = None
    if c.ext_fnames:
        # Every module reference goes in, including a script calling back into itself: the DOG compiler records
        # f@own_script too. Filtering self-references out made the ELIF chunk vanish (or list fewer names) for the six
        # vanilla scripts that call themselves - the last difference against the oracle (688 -> 694 of 694).
        mods = list(c.ext_fnames)
        if mods:
            elif_names = _elif_order(mods)

    data = DW.emit_scb(rtsn_lits=c.strings, rtss_names=names,
                       bmys_payload=bytes(bmys) if bmys else b'',
                       ysas_payload=bytes(ysas) if ysas else b'',
                       edoc_code=code, exports=n_exp,
                       trpx_payload=bytes(trpx) if n_exp else None,
                       elif_names=elif_names)
    return data


# ============================================================ runtime lint
# Opt-in semantic checks against the engine rules recovered from the vanilla
# bootfile.dol (see scb_work/api/dogruntime.py).  Does NOT change byte output;
# it only parses the source and reports whether the runtime (not dogcc) would
# accept the calls the script makes.
def lint_source(text, src='<input>', quiet_unknown=False):
    """Parse `text` and report runtime-level call problems.

    Returns a list of (kind, name, detail) diagnostics:
      'engine-arity' - a fixed-argc engine function is called with the wrong
                       number of args (the runtime rejects this at dispatch).
      'engine-name'  - a call marked as an engine/global call whose name is not
                       a statically-registered engine callable (only reported
                       when not quiet; may be a module function, so it is not
                       an error).
    The checks use the DOL engine registry + per-class method tables.
    """
    import json as _json
    _api = os.path.join(os.path.dirname(__file__), '..', 'api')
    if _api not in sys.path:
        sys.path.insert(0, _api)
    import dogruntime

    c = Ctx()
    Parser(c, text, src).parse_program()   # no decoration/patching needed
    db = dogruntime.db()
    defined = {fid for fid, _ in c.funcs}
    issues = []

    def sname(sid):
        return c.syms[sid].name if 0 <= sid < len(c.syms) else '?'

    for ins in c.ins:
        if ins.op == OP['CALL']:
            if ins.args[0] == 2:
                sid = ins.args[1]
                nm = sname(sid)
                if '@' in nm:
                    continue                    # module-qualified: not engine
                n = ins.args[2]
                rec = db.engine_lookup(nm)
                if rec and db.engine_argc_mismatch(nm, n):
                    issues.append(('engine-arity', nm,
                                   'runtime expects %d arg(s), got %d'
                                   % (rec['argc'], n)))
            continue
        if ins.op != OP['CALLFUNC']:
            continue
        callee = sname(ins.args[0])
        n = ins.args[1]
        obj2 = ins.args[2]
        if obj2 == 0x40000000:
            # engine / global call
            if '@' in callee:
                continue
            if callee in defined:
                continue                        # local function, not engine
            rec = db.engine_lookup(callee)
            if rec is None:
                if not quiet_unknown and ins.fn is not None:
                    issues.append(('engine-name', callee,
                                   'not a statically-registered DOL engine '
                                   'function (may be a dynamically provided '
                                   'engine func or cross-module script fn)'))
                continue
            if db.engine_argc_mismatch(callee, n):
                issues.append(('engine-arity', callee,
                               'runtime expects %d arg(s), got %d'
                               % (rec['argc'], n)))
    return issues


def main(argv):
    if len(argv) >= 2 and argv[1] == '--lint':
        # dogcompile.py --lint file.scp [file.scp ...]
        bad = 0
        for path in argv[2:]:
            text = open(path, encoding='utf8', errors='replace').read()
            try:
                issues = lint_source(text, path)
            except PErr as e:
                print('%s: parse error: %s' % (path, e))
                bad += 1
                continue
            if not issues:
                print('%s: ok' % path)
            for kind, nm, detail in issues:
                print('%s: %s: %s: %s' % (path, kind, nm, detail))
                bad += 1
        return 1 if bad else 0
    src, dst = argv[1], argv[2]
    text = open(src, encoding='utf8', errors='replace').read()
    data = compile_source(text, src)
    with open(dst, 'wb') as f:
        f.write(data)
    print(f"{src} -> {dst} ({len(data)} bytes)")


if __name__ == '__main__':
    main(sys.argv)
