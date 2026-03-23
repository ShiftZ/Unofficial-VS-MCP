#define BOOST_TEST_MODULE BoostTestSample
#define BOOST_TEST_NO_LIB
#include <boost/test/unit_test.hpp>

BOOST_AUTO_TEST_SUITE(ArithmeticSuite)

BOOST_AUTO_TEST_CASE(Addition)
{
    BOOST_CHECK_EQUAL(4, 2 + 2);
}

BOOST_AUTO_TEST_CASE(Subtraction)
{
    BOOST_CHECK_EQUAL(0, 2 - 2);
}

BOOST_AUTO_TEST_SUITE_END()

BOOST_AUTO_TEST_SUITE(StringSuite)

BOOST_AUTO_TEST_CASE(Comparison)
{
    BOOST_CHECK_EQUAL(std::string("hello"), std::string("hello"));
}

BOOST_AUTO_TEST_SUITE_END()
