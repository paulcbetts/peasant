﻿using Akavache;
using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using ReactiveUI;
using System.Reactive;

namespace Peasant.Models.Tests
{
    public static class TestBuild
    {
        public const string RepoUrl = "https://github.com/paulcbetts/peasant";
        public const string BuildScriptUrl = "https://github.com/paulcbetts/peasant/blob/master/script/cibuild.ps1";

        public const string PassingBuildSHA1 = "46c20227bb08185215f5b3d9519297142873b261";
        public const string FailingBecauseOfMsbuildSHA1 = "46c20227bb08185215f5b3d9519297142873b261";
    }

    public class BuildQueueIntegrationTests
    {
        [Fact]
        public async Task FullBuildIntegrationTest()
        {
            var cache = new TestBlobCache();
            var client = new GitHubClient(new ProductHeaderValue("Peasant"));

            var fixture = new BuildQueue(client, cache);
            using (fixture.Start()) {
                var result = await fixture.Enqueue(TestBuild.RepoUrl, TestBuild.PassingBuildSHA1, TestBuild.BuildScriptUrl);
            }
        }

        [Fact]
        public async Task ProcessSingleBuildIntegrationTest()
        {
            var cache = new TestBlobCache();
            var client = new GitHubClient(new ProductHeaderValue("Peasant"));
            var stdout = new Subject<string>();
            var allLines = stdout.CreateCollection();

            var fixture = new BuildQueue(client, cache);
            var result = await fixture.ProcessSingleBuild(new BuildQueueItem() {
                BuildId = 1,
                BuildScriptUrl = TestBuild.BuildScriptUrl,
                RepoUrl = TestBuild.RepoUrl,
                SHA1 = TestBuild.PassingBuildSHA1,
            }, stdout);

            var output = allLines.Aggregate(new StringBuilder(), (acc, x) => { acc.AppendLine(x); return acc; }).ToString();
            Console.WriteLine(output);

            Assert.Equal(0, result);
            Assert.False(String.IsNullOrWhiteSpace(output));
        }

        [Fact]
        public async Task ProcessSingleBuildThatFails()
        {
            var cache = new TestBlobCache();
            var client = new GitHubClient(new ProductHeaderValue("Peasant"));
            var stdout = new Subject<string>();
            var allLines = stdout.CreateCollection();

            var fixture = new BuildQueue(client, cache);
            var result = default(int);
            bool shouldDie = true;

            try {
                // NB: This build fails because NuGet package restore wasn't set 
                // up properly, so MSBuild is missing a ton of assemblies
                result = await fixture.ProcessSingleBuild(new BuildQueueItem() {
                    BuildId = 1,
                    BuildScriptUrl = TestBuild.BuildScriptUrl,
                    RepoUrl = TestBuild.RepoUrl,
                    SHA1 = TestBuild.FailingBecauseOfMsbuildSHA1,
                }, stdout);
            } catch (Exception ex) {
                Console.WriteLine(ex.ToString());
                shouldDie = false;
            }

            var output = allLines.Aggregate(new StringBuilder(), (acc, x) => { acc.AppendLine(x); return acc; }).ToString();
            Console.WriteLine(output);

            Assert.False(shouldDie);
        }
    }

    public class BuildQueueTests
    {
        [Fact]
        public async Task BuildsThatFailShouldBeRecorded()
        {
            var cache = new TestBlobCache();
            var client = new GitHubClient(new ProductHeaderValue("Peasant"));

            var fixture = new BuildQueue(client, cache, async (q, o) => {
                throw new Exception("Didn't work lol");
            });

            fixture.Start();

            var queueItem = await fixture.Enqueue(TestBuild.RepoUrl, TestBuild.PassingBuildSHA1, TestBuild.BuildScriptUrl);

            Assert.NotNull(queueItem);
            Assert.False(queueItem.BuildSucceded.Value);

            fixture = new BuildQueue(client, cache);
            var result = await fixture.GetBuildOutput(queueItem.BuildId);

            Assert.True(result.Item1.Contains("Didn't work lol"));
            Assert.NotEqual(0, result.Item2);
        }

        [Fact]
        public async Task BuildsThatSucceedShouldBeRecorded()
        {
            var cache = new TestBlobCache();
            var client = new GitHubClient(new ProductHeaderValue("Peasant"));

            var fixture = new BuildQueue(client, cache, (q, o) => {
                return Task.FromResult(0);
            });

            fixture.Start();

            var queueItem = await fixture.Enqueue(TestBuild.RepoUrl, TestBuild.PassingBuildSHA1, TestBuild.BuildScriptUrl);

            Assert.NotNull(queueItem);
            Assert.True(queueItem.BuildSucceded.Value);

            fixture = new BuildQueue(client, cache);
            var result = await fixture.GetBuildOutput(queueItem.BuildId);

            Assert.Equal(0, result.Item2);
        }

        [Fact]
        public void PausingTheQueueShouldntLoseBuilds()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void FailTheBuildIfBuildScriptUrlIsBogus()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void FailTheBuildIfBuildScriptUrlIsValidBut404s()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void BuildOutputForQueuedBuildsShouldHaveTheBuildId()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public void BuildOutputForInProgressBuildsShouldHaveBuildOutput()
        {
            throw new NotImplementedException();
        }

        [Fact]
        public async Task BuildOutputForUnknownBuildsShouldThrow()
        {
            var cache = new TestBlobCache();
            var client = new GitHubClient(new ProductHeaderValue("Peasant"));

            var fixture = new BuildQueue(client, cache, (q, o) => {
                return Task.FromResult(0);
            });

            bool shouldDie = true;
            try {
                await fixture.GetBuildOutput(42);
            } catch (Exception) {
                shouldDie = false;
            }

            Assert.False(shouldDie);
        }
    }
}