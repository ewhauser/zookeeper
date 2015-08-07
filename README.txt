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
本来此客户端可以通过NuGet获取，如果会使用NuGet, 则可以使用命令Install-Package ZooKeeperNet（需要最新版本的NuGet）

如果不会，就去 NuGet官网了解http://docs.nuget.org/docs/start-here/using-the-package-manager-console

如果你想自己编译 你可以去GitHub下载源码https://github.com/ewhauser/zookeeper

donet编译时会报出Genrated里的文件无法打开，实际上刚开始是没有的；

因为我没学过java，所以我看不懂根目录下那些文件搞什么的，

不过最后在网上查了很多资料和源码里的说明文档

ewhauser-zookeeper-a52ff80\src\java\main\org\apache\jute\package.html

ewhauser-zookeeper-a52ff80\src\java\main\org\apache\jute\compiler\package.html,

 原来是hadoop的Rcc(是用JAVA编写的 源文件中可以找到)，这个东西作用是src下的zookeeper.jute文件转换为C C++ java的数据结构 好像原来是没有C#的，是后来作者加上的，这里就先不管了，可以用就行，接下来说说怎么生成 ewhauser-zookeeper-a52ff80\src\dotnet\ZooKeeperNet\Generated的文件

我们需要运行ant命令

如果不知道ant，那google把

配置好ant 后 运行

ant -file build.xml



这样运行后等待build successfully  你的ewhauser-zookeeper-a52ff80\src\dotnet\ZooKeeperNet\Generated就有文件了

现在就能将zookeeperNet编译为Dll了

我编译的时候发现有MiscUtil.dll不存在的警告 ，所以我还是去把这个dll下载了下来

注意这个客户端必须要用.NET4.0编译

 以下我整理过的donet的源文件包

http://files.cnblogs.com/01-sunkey/dotnet.zip

感谢ewhauser
