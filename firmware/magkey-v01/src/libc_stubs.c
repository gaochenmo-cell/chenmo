/*
 * libc_stubs.c - 链接时不使用 crt0（-nostartfiles），启动文件仍会调用 __libc_init_array，
 * 后者需要 _init/_fini 两个空符号。固件中没有 C++ 构造函数，这里给出空实现即可。
 */
void _init(void) {}
void _fini(void) {}
