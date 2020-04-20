using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Mirror.Tests
{
    [TestFixture]
    public class SyncSetTest
    {
        public class SyncSetString : SyncHashSet<string> { }

        SyncSetString serverSyncSet;
        SyncSetString clientSyncSet;

        void SerializeAllTo<T>(T fromList, T toList) where T : SyncObject
        {
            NetworkWriter writer = new NetworkWriter();
            fromList.OnSerializeAll(writer);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            toList.OnDeserializeAll(reader);
        }

        void SerializeDeltaTo<T>(T fromList, T toList) where T : SyncObject
        {
            NetworkWriter writer = new NetworkWriter();
            fromList.OnSerializeDelta(writer);
            NetworkReader reader = new NetworkReader(writer.ToArray());
            toList.OnDeserializeDelta(reader);
            fromList.Flush();
        }

        [SetUp]
        public void SetUp()
        {
            serverSyncSet = new SyncSetString();
            clientSyncSet = new SyncSetString();

            // add some data to the list
            serverSyncSet.Add("Hello");
            serverSyncSet.Add("World");
            serverSyncSet.Add("!");
            SerializeAllTo(serverSyncSet, clientSyncSet);
        }

        [Test]
        public void TestInit()
        {
            Assert.That(serverSyncSet, Is.EquivalentTo(new[] { "Hello", "World", "!" }));
            Assert.That(clientSyncSet, Is.EquivalentTo(new[] { "Hello", "World", "!" }));
        }

        [Test]
        public void TestAdd()
        {
            serverSyncSet.Add("yay");
            Assert.That(serverSyncSet.IsDirty, Is.True);
            SerializeDeltaTo(serverSyncSet, clientSyncSet);
            Assert.That(clientSyncSet, Is.EquivalentTo(new[] { "Hello", "World", "!", "yay" }));
            Assert.That(serverSyncSet.IsDirty, Is.False);
        }

        [Test]
        public void TestClear()
        {
            serverSyncSet.Clear();
            Assert.That(serverSyncSet.IsDirty, Is.True);
            SerializeDeltaTo(serverSyncSet, clientSyncSet);
            Assert.That(clientSyncSet, Is.EquivalentTo(new string[] { }));
            Assert.That(serverSyncSet.IsDirty, Is.False);
        }

        [Test]
        public void TestRemove()
        {
            serverSyncSet.Remove("World");
            Assert.That(serverSyncSet.IsDirty, Is.True);
            SerializeDeltaTo(serverSyncSet, clientSyncSet);
            Assert.That(clientSyncSet, Is.EquivalentTo(new[] { "Hello", "!" }));
            Assert.That(serverSyncSet.IsDirty, Is.False);
        }

        [Test]
        public void TestMultSync()
        {
            serverSyncSet.Add("1");
            SerializeDeltaTo(serverSyncSet, clientSyncSet);
            // add some delta and see if it applies
            serverSyncSet.Add("2");
            SerializeDeltaTo(serverSyncSet, clientSyncSet);
            Assert.That(clientSyncSet, Is.EquivalentTo(new[] { "Hello", "World", "!", "1", "2" }));
        }

        [Test]
        public void CallbackTest()
        {
            bool called = false;

            clientSyncSet.Callback += (op, item) =>
            {
                called = true;

                Assert.That(op, Is.EqualTo(SyncSetString.Operation.OP_ADD));
                Assert.That(item, Is.EqualTo("yay"));
            };

            serverSyncSet.Add("yay");
            SerializeDeltaTo(serverSyncSet, clientSyncSet);

            Assert.That(called, Is.True);
        }

        [Test]
        public void CallbackRemoveTest()
        {
            bool called = false;

            clientSyncSet.Callback += (op, item) =>
            {
                called = true;

                Assert.That(op, Is.EqualTo(SyncSetString.Operation.OP_REMOVE));
                Assert.That(item, Is.EqualTo("World"));
            };
            serverSyncSet.Remove("World");
            SerializeDeltaTo(serverSyncSet, clientSyncSet);

            Assert.That(called, Is.True);
        }

        [Test]
        public void CountTest()
        {
            Assert.That(serverSyncSet.Count, Is.EqualTo(3));
        }

        [Test]
        public void ReadOnlyTest()
        {
            Assert.That(serverSyncSet.IsReadOnly, Is.False);
        }

        [Test]
        public void ReadonlyTest()
        {
            SyncSetString serverList = new SyncSetString();
            SyncSetString clientList = new SyncSetString();

            // data has been flushed,  should go back to clear
            Assert.That(clientList.IsReadOnly, Is.False);

            serverList.Add("1");
            serverList.Add("2");
            serverList.Add("3");
            SerializeDeltaTo(serverList, clientList);

            // client list should now lock itself,  trying to modify it
            // should produce an InvalidOperationException
            Assert.That(clientList.IsReadOnly, Is.True);
            Assert.Throws<InvalidOperationException>(() => { clientList.Add("5"); });
        }

        [Test]
        public void TestExceptWith()
        {
            serverSyncSet.ExceptWith(new[] { "World", "Hello" });
            SerializeDeltaTo(serverSyncSet, clientSyncSet);
            Assert.That(clientSyncSet, Is.EquivalentTo(new[] { "!" }));
        }

        [Test]
        public void TestExceptWithSelf()
        {
            serverSyncSet.ExceptWith(serverSyncSet);
            SerializeDeltaTo(serverSyncSet, clientSyncSet);
            Assert.That(clientSyncSet, Is.EquivalentTo(new String[] { }));
        }

        [Test]
        public void TestIntersectWith()
        {
            serverSyncSet.IntersectWith(new[] { "World", "Hello" });
            SerializeDeltaTo(serverSyncSet, clientSyncSet);
            Assert.That(clientSyncSet, Is.EquivalentTo(new[] { "World", "Hello" }));
        }

        [Test]
        public void TestIntersectWithSet()
        {
            serverSyncSet.IntersectWith(new HashSet<string> { "World", "Hello" });
            SerializeDeltaTo(serverSyncSet, clientSyncSet);
            Assert.That(clientSyncSet, Is.EquivalentTo(new[] { "World", "Hello" }));
        }

        [Test]
        public void TestIsProperSubsetOf()
        {
            Assert.That(clientSyncSet.IsProperSubsetOf(new[] { "World", "Hello", "!", "pepe" }));
        }

        [Test]
        public void TestIsProperSubsetOfSet()
        {
            Assert.That(clientSyncSet.IsProperSubsetOf(new HashSet<string> { "World", "Hello", "!", "pepe" }));
        }

        [Test]
        public void TestIsNotProperSubsetOf()
        {
            Assert.That(clientSyncSet.IsProperSubsetOf(new[] { "World", "!", "pepe" }), Is.False);
        }

        [Test]
        public void TestIsProperSuperSetOf()
        {
            Assert.That(clientSyncSet.IsProperSupersetOf(new[] { "World", "Hello" }));
        }

        [Test]
        public void TestIsSubsetOf()
        {
            Assert.That(clientSyncSet.IsSubsetOf(new[] { "World", "Hello", "!" }));
        }

        [Test]
        public void TestIsSupersetOf()
        {
            Assert.That(clientSyncSet.IsSupersetOf(new[] { "World", "Hello" }));
        }

        [Test]
        public void TestOverlaps()
        {
            Assert.That(clientSyncSet.Overlaps(new[] { "World", "my", "baby" }));
        }

        [Test]
        public void TestSetEquals()
        {
            Assert.That(clientSyncSet.SetEquals(new[] { "World", "Hello", "!" }));
        }

        [Test]
        public void TestSymmetricExceptWith()
        {
            serverSyncSet.SymmetricExceptWith(new HashSet<string> { "Hello", "is" });
            SerializeDeltaTo(serverSyncSet, clientSyncSet);
            Assert.That(clientSyncSet, Is.EquivalentTo(new[] { "World", "is", "!" }));
        }

        [Test]
        public void TestSymmetricExceptWithSelf()
        {
            serverSyncSet.SymmetricExceptWith(serverSyncSet);
            SerializeDeltaTo(serverSyncSet, clientSyncSet);
            Assert.That(clientSyncSet, Is.EquivalentTo(new String[] { }));
        }

        [Test]
        public void TestUnionWith()
        {
            serverSyncSet.UnionWith(new HashSet<string> { "Hello", "is" });
            SerializeDeltaTo(serverSyncSet, clientSyncSet);
            Assert.That(clientSyncSet, Is.EquivalentTo(new[] { "World", "Hello", "is", "!" }));
        }

        [Test]
        public void TestUnionWithSelf()
        {
            serverSyncSet.UnionWith(serverSyncSet);
            SerializeDeltaTo(serverSyncSet, clientSyncSet);
            Assert.That(clientSyncSet, Is.EquivalentTo(new[] { "World", "Hello", "!" }));
        }


        [Test]
        public void ResetShouldSetReadOnlyToFalse()
        {
            SyncSetString serverList = new SyncSetString();
            SyncSetString clientList = new SyncSetString();

            // data has been flushed,  should go back to clear
            Assert.That(clientList.IsReadOnly, Is.False);

            serverList.Add("1");
            serverList.Add("2");
            serverList.Add("3");
            SerializeDeltaTo(serverList, clientList);

            // client list should now lock itself,  trying to modify it
            // should produce an InvalidOperationException
            Assert.That(clientList.IsReadOnly, Is.True);
            Assert.Throws<InvalidOperationException>(() => { clientList.Add("5"); });

            clientList.Reset();

            // make old client the host

            SyncSetString hostList = clientList;
            SyncSetString clientList2 = new SyncSetString();


            Assert.That(hostList.IsReadOnly, Is.False);
            Assert.That(clientList2.IsReadOnly, Is.False);

            hostList.Add("1");
            hostList.Add("2");
            hostList.Add("3");
            SerializeDeltaTo(hostList, clientList2);

            // client list should now lock itself,  trying to modify it
            // should produce an InvalidOperationException
            Assert.That(clientList2.IsReadOnly, Is.True);
            Assert.That(hostList.IsReadOnly, Is.False);
            Assert.Throws<InvalidOperationException>(() => { clientList2.Add("5"); });
        }

        [Test]
        public void ResetShouldClearChanges()
        {
            SyncSetString serverList = new SyncSetString();

            serverList.Add("1");
            serverList.Add("2");
            serverList.Add("3");

            Assert.That(serverList.GetChangeCount(), Is.GreaterThan(0));

            serverList.Reset();

            Assert.That(serverList.GetChangeCount(), Is.Zero);
        }

        [Test]
        public void ResetShouldClearItems()
        {
            SyncSetString serverList = new SyncSetString();

            serverList.Add("1");
            serverList.Add("2");
            serverList.Add("3");

            Assert.That(serverList.Count, Is.GreaterThan(0));

            serverList.Reset();

            Assert.That(serverList.Count, Is.Zero);
        }
    }
}
