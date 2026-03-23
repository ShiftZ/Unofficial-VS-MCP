#include <gtest/gtest.h>

TEST(ArithmeticTest, Addition)
{
    EXPECT_EQ(4, 2 + 2);
}

TEST(ArithmeticTest, Subtraction)
{
    EXPECT_EQ(0, 2 - 2);
}

TEST(StringTest, Comparison)
{
    EXPECT_EQ(std::string("hello"), std::string("hello"));
}