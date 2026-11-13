grammar Spreadsheet;

// Варіант 6: бінарні + - * /, mod, div, ^ (піднесення у степінь), max(x,y), min(x,y)

compileUnit
    : expression EOF
    ;

expression
    : LPAREN expression RPAREN                               # ParenthesizedExpr
    | func=(MAX | MIN) LPAREN expression COMMA expression RPAREN  # MaxMinExpr
    | <assoc=right> expression EXPONENT expression           # ExponentialExpr
    | expression operatorToken=(MULTIPLY | DIVIDE | MOD | DIV) expression # MultiplicativeExpr
    | expression operatorToken=(ADD | SUBTRACT) expression   # AdditiveExpr
    | NUMBER                                                 # NumberExpr
    | IDENTIFIER                                             # IdentifierExpr
    ;

MAX : M A X ;
MIN : M I N ;
MOD : M O D ;
DIV : D I V ;

NUMBER     : [0-9]+ ;
IDENTIFIER : [a-zA-Z]+ [1-9] [0-9]* ;

EXPONENT : '^' ;
MULTIPLY : '*' ;
DIVIDE   : '/' ;
ADD      : '+' ;
SUBTRACT : '-' ;
LPAREN   : '(' ;
RPAREN   : ')' ;
COMMA    : ',' ;

WS : [ \t\r\n]+ -> skip ;

fragment A : [aA] ;
fragment D : [dD] ;
fragment I : [iI] ;
fragment M : [mM] ;
fragment N : [nN] ;
fragment O : [oO] ;
fragment V : [vV] ;
fragment X : [xX] ;
