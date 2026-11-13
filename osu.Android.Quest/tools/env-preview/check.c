#include <string.h>
#include <EGL/egl.h>
#include <EGL/eglext.h>
#include <GLES3/gl3.h>
#include <stdio.h>
#include <stdlib.h>
static char *readfile(const char *p){FILE*f=fopen(p,"rb");if(!f)return 0;fseek(f,0,2);long n=ftell(f);fseek(f,0,0);char*b=malloc(n+1);fread(b,1,n,f);b[n]=0;fclose(f);return b;}
int main(int argc,char**argv){
  PFNEGLGETPLATFORMDISPLAYEXTPROC getPlatformDisplay=(void*)eglGetProcAddress("eglGetPlatformDisplayEXT");
  EGLDisplay d=getPlatformDisplay?getPlatformDisplay(EGL_PLATFORM_SURFACELESS_MESA,EGL_DEFAULT_DISPLAY,0):eglGetDisplay(EGL_DEFAULT_DISPLAY);
  if(!eglInitialize(d,0,0)){printf("no egl\n");return 2;}
  eglBindAPI(EGL_OPENGL_ES_API);
  EGLint ca[]={EGL_CONTEXT_MAJOR_VERSION,3,EGL_CONTEXT_MINOR_VERSION,0,EGL_NONE};
  EGLConfig cfg; EGLint n; EGLint attrs[]={EGL_RENDERABLE_TYPE,EGL_OPENGL_ES3_BIT,EGL_NONE};
  eglChooseConfig(d,attrs,&cfg,1,&n);
  EGLContext c=eglCreateContext(d,n?cfg:EGL_NO_CONFIG_KHR,EGL_NO_CONTEXT,ca);
  if(!c||!eglMakeCurrent(d,EGL_NO_SURFACE,EGL_NO_SURFACE,c)){printf("no ctx\n");return 2;}
  printf("GL_VERSION: %s\n",glGetString(GL_VERSION));
  int fails=0; GLuint vs=0;
  for(int i=1;i<argc;i++){
    char*src=readfile(argv[i]); int isv=strstr(argv[i],".vert")!=0;
    GLuint s=glCreateShader(isv?GL_VERTEX_SHADER:GL_FRAGMENT_SHADER); glShaderSource(s,1,(const char**)&src,0); glCompileShader(s);
    GLint ok; glGetShaderiv(s,GL_COMPILE_STATUS,&ok); char log[4096]; glGetShaderInfoLog(s,4096,0,log);
    printf("%s %s %s\n",ok?"OK  ":"FAIL",argv[i],ok?"":log); if(!ok)fails++;
    if(isv) vs=s; else if(ok&&vs){GLuint p=glCreateProgram();glAttachShader(p,vs);glAttachShader(p,s);glLinkProgram(p);glGetProgramiv(p,GL_LINK_STATUS,&ok);glGetProgramInfoLog(p,4096,0,log);printf("  link %s %s\n",ok?"OK":"FAIL",ok?"":log);if(!ok)fails++;}
  }
  return fails;
}
