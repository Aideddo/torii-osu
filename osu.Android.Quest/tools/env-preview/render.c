// Offline preview of the VR environment shaders: renders one eye's view of the scene into a PPM, and runs the dominant colour pass.
#include <string.h>
#include <EGL/egl.h>
#include <EGL/eglext.h>
#include <GLES3/gl3.h>
#include <stdio.h>
#include <stdlib.h>
#include <math.h>
static char *readfile(const char *p){FILE*f=fopen(p,"rb");fseek(f,0,2);long n=ftell(f);fseek(f,0,0);char*b=malloc(n+1);fread(b,1,n,f);b[n]=0;fclose(f);return b;}
static GLuint prog(const char*vs,const char*fs){GLuint p=glCreateProgram();const char*srcs[2]={readfile(vs),readfile(fs)};GLenum t[2]={GL_VERTEX_SHADER,GL_FRAGMENT_SHADER};
 for(int i=0;i<2;i++){GLuint s=glCreateShader(t[i]);glShaderSource(s,1,&srcs[i],0);glCompileShader(s);glAttachShader(p,s);}glLinkProgram(p);return p;}
static GLuint tex(int w,int h,const unsigned char*data){GLuint t;glGenTextures(1,&t);glBindTexture(GL_TEXTURE_2D,t);glTexImage2D(GL_TEXTURE_2D,0,GL_RGBA8,w,h,0,GL_RGBA,GL_UNSIGNED_BYTE,data);
 glTexParameteri(GL_TEXTURE_2D,GL_TEXTURE_MIN_FILTER,GL_LINEAR);glTexParameteri(GL_TEXTURE_2D,GL_TEXTURE_MAG_FILTER,GL_LINEAR);glTexParameteri(GL_TEXTURE_2D,GL_TEXTURE_WRAP_S,GL_CLAMP_TO_EDGE);glTexParameteri(GL_TEXTURE_2D,GL_TEXTURE_WRAP_T,GL_CLAMP_TO_EDGE);return t;}
#define U(p,n) glGetUniformLocation(p,n)
int main(int argc,char**argv){
  PFNEGLGETPLATFORMDISPLAYEXTPROC gpd=(void*)eglGetProcAddress("eglGetPlatformDisplayEXT");
  EGLDisplay d=gpd(EGL_PLATFORM_SURFACELESS_MESA,EGL_DEFAULT_DISPLAY,0); eglInitialize(d,0,0); eglBindAPI(EGL_OPENGL_ES_API);
  EGLint ca[]={EGL_CONTEXT_MAJOR_VERSION,3,EGL_NONE}; EGLConfig cfg; EGLint n; EGLint at[]={EGL_RENDERABLE_TYPE,EGL_OPENGL_ES3_BIT,EGL_NONE}; eglChooseConfig(d,at,&cfg,1,&n);
  EGLContext c=eglCreateContext(d,n?cfg:EGL_NO_CONFIG_KHR,EGL_NO_CONTEXT,ca); eglMakeCurrent(d,EGL_NO_SURFACE,EGL_NO_SURFACE,c);
  GLuint vao; glGenVertexArrays(1,&vao); glBindVertexArray(vao);

  // Synthetic background (16:9): mostly warm orange with a teal region and dark areas; premultiplied opaque.
  int BW=192,BH=108; unsigned char*bg=malloc(BW*BH*4);
  for(int y=0;y<BH;y++)for(int x=0;x<BW;x++){unsigned char*p=bg+(y*BW+x)*4;float fx=x/(float)BW,fy=y/(float)BH;
    float r=0.95f*(1-fy*0.6f),g=0.45f*(1-fy*0.5f),b=0.12f; if(fx>0.72f){r=0.1f;g=0.6f;b=0.65f;} if(fy<0.15f){r*=0.2f;g*=0.2f;b*=0.2f;}
    p[0]=r*255;p[1]=g*255;p[2]=b*255;p[3]=255;}
  GLuint bgTex=tex(BW,BH,bg);

  // Dominant colour pass.
  const char*dir=argv[1]; char vs[512],fs[512];
  sprintf(vs,"%s/fullscreen_vertex.vert",dir); sprintf(fs,"%s/dominant_fragment.frag",dir);
  GLuint pd=prog(vs,fs); GLuint domTex=tex(1,1,0); GLuint fb; glGenFramebuffers(1,&fb); glBindFramebuffer(GL_FRAMEBUFFER,fb);
  glFramebufferTexture2D(GL_FRAMEBUFFER,GL_COLOR_ATTACHMENT0,GL_TEXTURE_2D,domTex,0); glViewport(0,0,1,1);
  glUseProgram(pd); glActiveTexture(GL_TEXTURE0); glBindTexture(GL_TEXTURE_2D,bgTex); glUniform1i(U(pd,"u_image"),0); glDrawArrays(GL_TRIANGLES,0,3);
  unsigned char px[4]; glReadPixels(0,0,1,1,GL_RGBA,GL_UNSIGNED_BYTE,px); printf("dominant colour: %d %d %d confidence %d\n",px[0],px[1],px[2],px[3]);

  // Scene: one eye looking straight ahead, eye 1.4 m above the floor.
  int W=1176,H=1232; sprintf(fs,"%s/scene_fragment.frag",dir); GLuint ps=prog(vs,fs);
  GLuint out=tex(W,H,0); glFramebufferTexture2D(GL_FRAMEBUFFER,GL_COLOR_ATTACHMENT0,GL_TEXTURE_2D,out,0); glViewport(0,0,W,H);
  unsigned char lum=argc>5?atoi(argv[5]):160; unsigned char one[4]={lum,lum,lum,255}; /* background luminance (dim) */ GLuint lumTex=tex(1,1,one);
  unsigned char amb[4]={px[0],px[1],px[2],255}; GLuint ambTex=tex(1,1,amb);
  glUseProgram(ps);
  float pitch=atof(argv[3])*M_PI/180; // look direction pitch for the preview camera
  glUniform3f(U(ps,"u_eye"),0,0,0); glUniform3f(U(ps,"u_right"),1,0,0); glUniform3f(U(ps,"u_up"),0,cosf(pitch),sinf(pitch)); glUniform3f(U(ps,"u_back"),0,-sinf(pitch),cosf(pitch));
  glUniform4f(U(ps,"u_tan"),-tanf(50*M_PI/180),tanf(45*M_PI/180),-tanf(55*M_PI/180),tanf(45*M_PI/180));
  int field=atoi(argv[2]); glUniform1f(U(ps,"u_field"),field);
  glUniform1f(U(ps,"u_floorY"),-1.4f); glUniform4f(U(ps,"u_grid"),0,0,1,0);
  float D=10,ww=2*D*tanf(69*M_PI/360),wh=ww*9/16; glUniform3f(U(ps,"u_wallCentre"),0,wh/2-1.4f,-D); glUniform3f(U(ps,"u_wallRight"),1,0,0); glUniform3f(U(ps,"u_wallUp"),0,1,0);
  glUniform2f(U(ps,"u_wallSize"),ww,wh); glUniform3f(U(ps,"u_glowDir"),0,0,-1);
  if(field)glUniform4f(U(ps,"u_halo"),0,(wh/2-1.4f)/D,tanf(69*M_PI/360),wh/2/D);
  else glUniform4f(U(ps,"u_halo"),0,0,81*M_PI/360,81*M_PI/360*9/16);
  glUniform1f(U(ps,"u_hasWall"),1); glUniform1f(U(ps,"u_flashReduction"),1); glUniform1f(U(ps,"u_pulse"),0.45f); glUniform1f(U(ps,"u_kiai"),0);
  float bands[32]; for(int i=0;i<32;i++) bands[i]=0.5f+0.45f*sinf(i*0.7f); glUniform1fv(U(ps,"u_bands"),32,bands);
  glActiveTexture(GL_TEXTURE1); glBindTexture(GL_TEXTURE_2D,lumTex); glActiveTexture(GL_TEXTURE2); glBindTexture(GL_TEXTURE_2D,lumTex);
  glActiveTexture(GL_TEXTURE3); glBindTexture(GL_TEXTURE_2D,ambTex); glActiveTexture(GL_TEXTURE0); glBindTexture(GL_TEXTURE_2D,bgTex);
  glUniform1i(U(ps,"u_wallTex"),0); glUniform1i(U(ps,"u_adapted"),1); glUniform1i(U(ps,"u_current"),2); glUniform1i(U(ps,"u_ambient"),3);
  glDrawArrays(GL_TRIANGLES,0,3);
  unsigned char*img=malloc(W*H*4); glReadPixels(0,0,W,H,GL_RGBA,GL_UNSIGNED_BYTE,img);
  FILE*f=fopen(argv[4],"wb"); fprintf(f,"P6\n%d %d\n255\n",W,H); for(int y=H-1;y>=0;y--)for(int x=0;x<W;x++)fwrite(img+(y*W+x)*4,1,3,f); fclose(f);
  printf("GL error: 0x%x\n",glGetError());
  return 0;
}
