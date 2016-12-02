A very good to start book "Oreilly.ZooKeeper.Distributed.Process.Coordination"

For the latest information about ZooKeeper, please visit our website at:

   http://zookeeper.apache.org/

and our wiki, at:

   https://cwiki.apache.org/confluence/display/ZOOKEEPER

Full documentation for this release can also be found in docs/index.html

---------------------------
Packaging/release artifacts

The release artifact contains the following jar file at the toplevel:

zookeeper-<version>.jar         - legacy jar file which contains all classes
                                  and source files. Prior to version 3.3.0 this
                                  was the only jar file available. It has the 
                                  benefit of having the source included (for
                                  debugging purposes) however is also larger as
                                  a result

The release artifact contains the following jar files in "dist-maven" directory:

zookeeper-<version>.jar         - bin (binary) jar - contains only class (*.class) files
zookeeper-<version>-sources.jar - contains only src (*.java) files
zookeeper-<version>-javadoc.jar - contains only javadoc files

These bin/src/javadoc jars were added specifically to support Maven/Ivy which have 
the ability to pull these down automatically as part of your build process. 
The content of the legacy jar and the bin+sources jar are the same.

As of version 3.3.0 bin/sources/javadoc jars contained in dist-maven directory
are deployed to the Apache Maven repository after the release has been accepted
by Apache:
  http://people.apache.org/repo/m2-ibiblio-rsync-repository/


###################################################################################
http://www.cnblogs.com/01-sunkey/articles/2438538.html

Zookeeper .Net Client
�����˿ͻ��˿���ͨ��NuGet��ȡ�������ʹ��NuGet, �����ʹ������Install-Package ZooKeeperNet����Ҫ���°汾��NuGet��

������ᣬ��ȥ NuGet�����˽�http://docs.nuget.org/docs/start-here/using-the-package-manager-console

��������Լ����� �����ȥGitHub����Դ��https://github.com/ewhauser/zookeeper

donet����ʱ�ᱨ��Genrated����ļ��޷��򿪣�ʵ���ϸտ�ʼ��û�еģ�

��Ϊ��ûѧ��java�������ҿ�������Ŀ¼����Щ�ļ���ʲô�ģ�

������������ϲ��˺ܶ����Ϻ�Դ�����˵���ĵ�

ewhauser-zookeeper-a52ff80\src\java\main\org\apache\jute\package.html

ewhauser-zookeeper-a52ff80\src\java\main\org\apache\jute\compiler\package.html,

 ԭ����hadoop��Rcc(����JAVA��д�� Դ�ļ��п����ҵ�)���������������src�µ�zookeeper.jute�ļ�ת��ΪC C++ java�����ݽṹ ����ԭ����û��C#�ģ��Ǻ������߼��ϵģ�������Ȳ����ˣ������þ��У�������˵˵��ô���� ewhauser-zookeeper-a52ff80\src\dotnet\ZooKeeperNet\Generated���ļ�

������Ҫ����ant����

�����֪��ant����google��

���ú�ant �� ����

ant -file build.xml



�������к�ȴ�build successfully  ���ewhauser-zookeeper-a52ff80\src\dotnet\ZooKeeperNet\Generated�����ļ���

���ھ��ܽ�zookeeperNet����ΪDll��

�ұ����ʱ������MiscUtil.dll�����ڵľ��� �������һ���ȥ�����dll����������

ע������ͻ��˱���Ҫ��.NET4.0����

 �������������donet��Դ�ļ���

http://files.cnblogs.com/01-sunkey/dotnet.zip

��лewhauser
