#include "CppUnitTest.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

TEST_CLASS(MsUnitTestSample)
{
public:
    TEST_METHOD(Addition)
    {
        Assert::AreEqual(4, 2 + 2);
    }

    TEST_METHOD(Subtraction)
    {
        Assert::AreEqual(0, 2 - 2);
    }

    TEST_METHOD(StringComparison)
    {
        Assert::AreEqual(L"hello", L"hello");
    }
};
